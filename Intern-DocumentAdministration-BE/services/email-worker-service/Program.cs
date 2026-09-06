using EmailWorkerService;
using EmailWorkerService.Data;
using EmailWorkerService.Models;
using EmailWorkerService.Services;
using EmailWorkerService.Services.Integration;
using MailKit.Net.Imap;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Text;
using System.Threading;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("ConnectionStrings:Default is required for EmailWorkerService.");
}

builder.Services.AddDbContext<EmailWorkerDbContext>(options =>
{
    if (connectionString.Contains("Data Source=") && connectionString.EndsWith(".db"))
    {
        var fileName = connectionString.Replace("Data Source=", "").Trim();
        if (!Path.IsPathRooted(fileName))
        {
            var absoluteDbPath = Path.Combine(builder.Environment.ContentRootPath, fileName);
            connectionString = $"Data Source={absoluteDbPath}";
        }
        options.UseSqlite(connectionString);
    }
    else
    {
        options.UseSqlServer(connectionString, sqlOptions =>
            sqlOptions.EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), null));
    }
});

var jwtSecret = builder.Configuration["Jwt:Secret"];
if (string.IsNullOrWhiteSpace(jwtSecret) || Encoding.UTF8.GetByteCount(jwtSecret) < 32)
{
    throw new InvalidOperationException("Jwt:Secret must be configured with at least 32 bytes.");
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddSingleton<InternalServiceTokenProvider>();
builder.Services.AddScoped<IEmailProcessor, EmailProcessor>();
builder.Services.AddHttpClient<IFilesServiceClient, FilesServiceClient>();
builder.Services.AddHttpClient<IDocumentServiceClient, DocumentServiceClient>();
builder.Services.AddHttpClient<IAiOcrServiceClient, AiOcrServiceClient>();
builder.Services.AddHostedService<EmailBackgroundWorker>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<EmailWorkerDbContext>();
    db.Database.EnsureCreated();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new
{
    success = true,
    data = new { status = "healthy", service = "email-worker-service" },
    message = (string?)null,
    errors = Array.Empty<string>()
}));

var emailWorker = app.MapGroup("/api/email-worker").RequireAuthorization();

emailWorker.MapGet("/settings", async (
    EmailWorkerDbContext db,
    IConfiguration configuration,
    CancellationToken cancellationToken) =>
{
    var settings = await GetOrCreateSettingsAsync(db, configuration, cancellationToken);

    return Results.Ok(new
    {
        success = true,
        data = ToSettingsResponse(settings),
        message = (string?)null,
        errors = Array.Empty<string>()
    });
})
.WithName("GetEmailImapSettings")
.WithOpenApi();

emailWorker.MapPut("/settings", async (
    SaveImapSettingsRequest request,
    EmailWorkerDbContext db,
    IConfiguration configuration,
    CancellationToken cancellationToken) =>
{
    var validationMessage = ValidateSettingsRequest(
        request.ImapHost,
        request.ImapPort,
        request.EmailAddress,
        request.AutoScanIntervalMinutes);

    if (validationMessage != null)
    {
        return Results.BadRequest(new
        {
            success = false,
            data = (object?)null,
            message = validationMessage,
            errors = Array.Empty<string>()
        });
    }

    var settings = await GetOrCreateSettingsAsync(db, configuration, cancellationToken);

    settings.ImapHost = request.ImapHost.Trim();
    settings.ImapPort = request.ImapPort;
    settings.UseSsl = request.UseSsl;
    settings.EmailAddress = request.EmailAddress.Trim();
    settings.WhitelistedDomains = request.WhitelistedDomains?.Trim() ?? string.Empty;
    settings.AutoScanIntervalMinutes = request.AutoScanIntervalMinutes;
    settings.UpdatedAt = DateTime.UtcNow;

    if (!string.IsNullOrWhiteSpace(request.AppPassword))
    {
        settings.AppPassword = request.AppPassword.Trim();
    }

    await db.SaveChangesAsync(cancellationToken);

    return Results.Ok(new
    {
        success = true,
        data = ToSettingsResponse(settings),
        message = "Mailbox settings saved successfully.",
        errors = Array.Empty<string>()
    });
})
.WithName("SaveEmailImapSettings")
.WithOpenApi();

emailWorker.MapPost("/settings/test-connection", async (
    TestImapConnectionRequest request,
    EmailWorkerDbContext db,
    IConfiguration configuration,
    CancellationToken cancellationToken) =>
{
    var validationMessage = ValidateSettingsRequest(
        request.ImapHost,
        request.ImapPort,
        request.EmailAddress,
        60);

    if (validationMessage != null)
    {
        return Results.BadRequest(new
        {
            success = false,
            data = (object?)null,
            message = validationMessage,
            errors = Array.Empty<string>()
        });
    }

    var savedSettings = await GetOrCreateSettingsAsync(db, configuration, cancellationToken);
    var password = request.AppPassword;

    if (string.IsNullOrWhiteSpace(password) ||
        password.Contains("saved password not resent", StringComparison.OrdinalIgnoreCase))
    {
        password = savedSettings.AppPassword;
    }

    if (string.IsNullOrWhiteSpace(password))
    {
        return Results.BadRequest(new
        {
            success = false,
            data = (object?)null,
            message = "App password is required to test the IMAP connection.",
            errors = Array.Empty<string>()
        });
    }

    try
    {
        using var client = new ImapClient();
        await client.ConnectAsync(
            request.ImapHost.Trim(),
            request.ImapPort,
            request.UseSsl,
            cancellationToken);
        await client.AuthenticateAsync(request.EmailAddress.Trim(), password, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);

        return Results.Ok(new
        {
            success = true,
            data = (object?)null,
            message = "IMAP connection successful.",
            errors = Array.Empty<string>()
        });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new
        {
            success = false,
            data = (object?)null,
            message = $"IMAP connection failed: {ex.Message}",
            errors = Array.Empty<string>()
        });
    }
})
.WithName("TestEmailImapConnection")
.WithOpenApi();

emailWorker.MapPost("/trigger-scan", (
    IServiceScopeFactory scopeFactory,
    ILogger<Program> logger) =>
{
    _ = Task.Run(async () =>
    {
        try
        {
            using var scope = scopeFactory.CreateScope();

            var processor =
                scope.ServiceProvider.GetRequiredService<IEmailProcessor>();

            await processor.ProcessIncomingEmailsAsync(
                "Manual",
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Manual email scan failed.");
        }
    });

    return Results.Accepted(
        "/api/email-worker/history",
        new
        {
            success = true,
            data = (object?)null,
            message = "Email scan started in background.",
            errors = Array.Empty<string>()
        });
})
.WithName("TriggerEmailScan")
.WithOpenApi();

// Backward-compatible alias for the old development endpoint.
emailWorker.MapPost("/trigger", async (
    IEmailProcessor processor,
    CancellationToken cancellationToken) =>
{
    var result = await processor.ProcessIncomingEmailsAsync("Manual", cancellationToken);
    return Results.Ok(new
    {
        success = result.Success,
        data = new
        {
            emailsScanned = result.EmailsScanned,
            documentsCreated = result.DocumentsCreated
        },
        message = result.Success ? "Email scan completed successfully." : result.ErrorMessage,
        errors = Array.Empty<string>()
    });
})
.WithName("TriggerEmailScanLegacy")
.WithOpenApi();

emailWorker.MapGet("/history", async (
    EmailWorkerDbContext db,
    int page = 1,
    int pageSize = 20,
    CancellationToken cancellationToken = default) =>
{
    page = Math.Max(page, 1);
    pageSize = Math.Clamp(pageSize <= 0 ? 20 : pageSize, 1, 100);

    var query = db.EmailScanLogs
        .AsNoTracking()
        .OrderByDescending(x => x.StartedAt);

    var totalCount = await query.CountAsync(cancellationToken);
    var items = await query
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .ToListAsync(cancellationToken);

    return Results.Ok(new
    {
        success = true,
        data = new { items, totalCount },
        message = (string?)null,
        errors = Array.Empty<string>()
    });
})
.WithName("GetEmailScanHistory")
.WithOpenApi();


emailWorker.MapDelete("/history", async (
    EmailWorkerDbContext db,
    CancellationToken cancellationToken) =>
{
    db.EmailScanItemLogs.RemoveRange(db.EmailScanItemLogs);
    db.EmailScanLogs.RemoveRange(db.EmailScanLogs);
    await db.SaveChangesAsync(cancellationToken);

    return Results.Ok(new
    {
        success = true,
        data = (object?)null,
        message = "Scan history cleared successfully.",
        errors = Array.Empty<string>()
    });
})
.WithName("ClearEmailScanHistory")
.WithOpenApi();

emailWorker.MapGet("/history/{scanId:guid}/items", async (
    Guid scanId,
    EmailWorkerDbContext db,
    CancellationToken cancellationToken) =>
{
    var scanLog = await db.EmailScanLogs
        .AsNoTracking()
        .FirstOrDefaultAsync(
            x => x.Id == scanId,
            cancellationToken);

    if (scanLog == null)
    {
        return Results.NotFound(new
        {
            success = false,
            data = (object?)null,
            message = "Scan history not found.",
            errors = Array.Empty<string>()
        });
    }

    var items = await db.EmailScanItemLogs
        .AsNoTracking()
        .Where(x => x.ScanLogId == scanId)
        .OrderByDescending(x => x.ReceivedAt)
        .ThenBy(x => x.AttachmentName)
        .ToListAsync(cancellationToken);

    return Results.Ok(new
    {
        success = true,
        data = new
        {
            scan = scanLog,
            items
        },
        message = (string?)null,
        errors = Array.Empty<string>()
    });
})
.WithName("GetEmailScanItems")
.WithOpenApi();


emailWorker.MapGet("/scan-items/{itemId:guid}", async (
    Guid itemId,
    EmailWorkerDbContext db,
    CancellationToken cancellationToken) =>
{
    var item = await db.EmailScanItemLogs
        .AsNoTracking()
        .FirstOrDefaultAsync(
            x => x.Id == itemId,
            cancellationToken);

    if (item == null)
    {
        return Results.NotFound(new
        {
            success = false,
            data = (object?)null,
            message = "Email scan item not found.",
            errors = Array.Empty<string>()
        });
    }

    return Results.Ok(new
    {
        success = true,
        data = item,
        message = (string?)null,
        errors = Array.Empty<string>()
    });
})
.WithName("GetEmailScanItem")
.WithOpenApi();


emailWorker.MapPost("/scan-items/{itemId:guid}/confirm-intake", async (
    Guid itemId,
    ConfirmIntakePayload? payload,
    EmailWorkerDbContext db,
    IDocumentServiceClient documentServiceClient,
    CancellationToken cancellationToken) =>
{
    var item = await db.EmailScanItemLogs
        .AsNoTracking()
        .FirstOrDefaultAsync(
            x => x.Id == itemId,
            cancellationToken);

    if (item == null)
    {
        return Results.NotFound(new
        {
            success = false,
            data = (object?)null,
            message = "Email scan item not found.",
            errors = Array.Empty<string>()
        });
    }

    if (!string.IsNullOrWhiteSpace(item.DocumentId)
        || string.Equals(
            item.Status,
            "IntakeCompleted",
            StringComparison.OrdinalIgnoreCase))
    {
        return Results.Ok(new
        {
            success = true,
            data = new
            {
                itemId = item.Id,
                documentId = item.DocumentId,
                status = item.Status,
                alreadyProcessed = true
            },
            message = "This email has already been saved to Documents.",
            errors = Array.Empty<string>()
        });
    }

    if (item.FileId == null)
    {
        return Results.BadRequest(new
        {
            success = false,
            data = (object?)null,
            message = "This scan item does not have a valid PDF file.",
            errors = Array.Empty<string>()
        });
    }

    var canConfirm =
        string.Equals(
            item.Status,
            "ReadyForIntake",
            StringComparison.OrdinalIgnoreCase)
        ||
        string.Equals(
            item.Status,
            "IntakeFailed",
            StringComparison.OrdinalIgnoreCase)
        ||
        string.Equals(
            item.Status,
            "IntakeProcessing",
            StringComparison.OrdinalIgnoreCase);

    if (!canConfirm)
    {
        return Results.BadRequest(new
        {
            success = false,
            data = new
            {
                itemId = item.Id,
                status = item.Status
            },
            message = $"Cannot save this item while status is '{item.Status}'.",
            errors = Array.Empty<string>()
        });
    }

    var claimedRows = await db.EmailScanItemLogs
        .Where(x =>
            x.Id == itemId
            && x.DocumentId == null
            && (
                x.Status == "ReadyForIntake"
                || x.Status == "IntakeFailed"
                || x.Status == "IntakeProcessing"
            ))
        .ExecuteUpdateAsync(
            setters => setters
                .SetProperty(
                    x => x.Status,
                    "IntakeProcessing")
                .SetProperty(
                    x => x.ErrorMessage,
                    (string?)null),
            cancellationToken);

    if (claimedRows == 0)
    {
        var latest = await db.EmailScanItemLogs
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == itemId,
                cancellationToken);

        if (latest != null
            && (
                !string.IsNullOrWhiteSpace(latest.DocumentId)
                || latest.Status == "IntakeCompleted"
            ))
        {
            return Results.Ok(new
            {
                success = true,
                data = new
                {
                    itemId = latest.Id,
                    documentId = latest.DocumentId,
                    status = latest.Status,
                    alreadyProcessed = true
                },
                message = "This email has already been saved to Documents.",
                errors = Array.Empty<string>()
            });
        }

        return Results.Conflict(new
        {
            success = false,
            data = new
            {
                itemId,
                status = latest?.Status
            },
            message = "This email is currently being processed.",
            errors = Array.Empty<string>()
        });
    }

    var docType = payload?.DocumentType ?? "incoming";
    var finalTitle = !string.IsNullOrWhiteSpace(payload?.Title)
        ? payload.Title.Trim()
        : !string.IsNullOrWhiteSpace(item.Subject)
            ? item.Subject.Trim()
            : !string.IsNullOrWhiteSpace(item.ExtractedSubject)
                ? item.ExtractedSubject.Trim()
                : $"Official dispatch from {item.SenderEmail}";

    var finalRefNumber = !string.IsNullOrWhiteSpace(payload?.ReferenceNumber)
        ? payload.ReferenceNumber.Trim()
        : item.ExtractedReferenceNumber;

    var finalPartnerId = payload?.PartnerId ?? item.PartnerId;

    try
    {
        var documentId =
            await documentServiceClient
                .RegisterDocumentAsync(
                    docType,
                    finalTitle,
                    finalRefNumber,
                    finalPartnerId,
                    item.FileId.Value,
                    item.ReceivedAt,
                    payload?.DepartmentId);

        if (string.IsNullOrWhiteSpace(documentId))
        {
            await db.EmailScanItemLogs
                .Where(x => x.Id == itemId)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(
                            x => x.Status,
                            "IntakeFailed")
                        .SetProperty(
                            x => x.ErrorMessage,
                            $"DocumentService could not create the {docType} document."),
                    CancellationToken.None);

            return Results.StatusCode(
                StatusCodes.Status502BadGateway);
        }

        var confirmedAt = DateTime.UtcNow;

        await db.EmailScanItemLogs
            .Where(x => x.Id == itemId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(
                        x => x.DocumentId,
                        documentId)
                    .SetProperty(
                        x => x.Status,
                        "IntakeCompleted")
                    .SetProperty(
                        x => x.ErrorMessage,
                        (string?)null)
                    .SetProperty(
                        x => x.IntakeConfirmedAt,
                        confirmedAt),
                CancellationToken.None);

        await db.EmailScanLogs
            .Where(x => x.Id == item.ScanLogId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(
                        x => x.DocumentsCreated,
                        x => x.DocumentsCreated + 1)
                    .SetProperty(
                        x => x.ReadyForIntakeCount,
                        x => x.ReadyForIntakeCount > 0
                            ? x.ReadyForIntakeCount - 1
                            : 0),
                CancellationToken.None);

        return Results.Ok(new
        {
            success = true,
            data = new
            {
                itemId = item.Id,
                documentId,
                status = "IntakeCompleted",
                intakeConfirmedAt = confirmedAt
            },
            message = "Document saved to Incoming Documents successfully.",
            errors = Array.Empty<string>()
        });
    }
    catch (Exception ex)
    {
        var errorMessage =
            ex.Message.Length <= 2000
                ? ex.Message
                : ex.Message.Substring(0, 2000);

        await db.EmailScanItemLogs
            .Where(x => x.Id == itemId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(
                        x => x.Status,
                        "IntakeFailed")
                    .SetProperty(
                        x => x.ErrorMessage,
                        errorMessage),
                CancellationToken.None);

        return Results.Problem(
            title: "Could not save incoming document.",
            detail: ex.Message,
            statusCode: StatusCodes.Status500InternalServerError);
    }
})
.WithName("ConfirmEmailScanIntake")
.WithOpenApi();

app.Run();

static async Task<EmailImapSettings> GetOrCreateSettingsAsync(
    EmailWorkerDbContext db,
    IConfiguration configuration,
    CancellationToken cancellationToken)
{
    var settings = await db.EmailImapSettings
        .FirstOrDefaultAsync(x => x.Id == 1, cancellationToken);

    if (settings != null)
    {
        return settings;
    }

    settings = new EmailImapSettings
    {
        Id = 1,
        ImapHost = configuration["ImapSettings:Host"] ?? "imap.gmail.com",
        ImapPort = int.TryParse(configuration["ImapSettings:Port"], out var port) ? port : 993,
        UseSsl = !bool.TryParse(configuration["ImapSettings:UseSSL"], out var useSsl) || useSsl,
        EmailAddress = configuration["ImapSettings:Email"] ?? string.Empty,
        AppPassword = configuration["ImapSettings:Password"] ?? string.Empty,
        WhitelistedDomains = configuration["ImapSettings:WhitelistedDomains"] ?? string.Empty,
        AutoScanIntervalMinutes = int.TryParse(
            configuration["ImapSettings:AutoScanIntervalMinutes"],
            out var interval)
            ? Math.Clamp(interval, 1, 1440)
            : 60,
        UpdatedAt = DateTime.UtcNow
    };

    db.EmailImapSettings.Add(settings);
    await db.SaveChangesAsync(cancellationToken);
    return settings;
}

static object ToSettingsResponse(EmailImapSettings settings) => new
{
    imapHost = settings.ImapHost,
    imapPort = settings.ImapPort,
    useSsl = settings.UseSsl,
    emailAddress = settings.EmailAddress,
    hasPassword = !string.IsNullOrWhiteSpace(settings.AppPassword),
    whitelistedDomains = settings.WhitelistedDomains,
    autoScanIntervalMinutes = settings.AutoScanIntervalMinutes,
    updatedAt = settings.UpdatedAt
};

static string? ValidateSettingsRequest(
    string? host,
    int port,
    string? emailAddress,
    int intervalMinutes)
{
    if (string.IsNullOrWhiteSpace(host))
    {
        return "IMAP host is required.";
    }

    if (port is < 1 or > 65535)
    {
        return "IMAP port must be between 1 and 65535.";
    }

    if (string.IsNullOrWhiteSpace(emailAddress) || !emailAddress.Contains('@'))
    {
        return "A valid intake email address is required.";
    }

    if (intervalMinutes is < 1 or > 1440)
    {
        return "Auto-scan interval must be between 1 and 1440 minutes.";
    }

    return null;
}

public class ConfirmIntakePayload
{
    public string? DocumentType { get; set; } = "incoming"; // "incoming" | "outgoing" | "internal"
    public string? Title { get; set; }
    public string? ReferenceNumber { get; set; }
    public Guid? PartnerId { get; set; }
    public Guid? DepartmentId { get; set; }
}
