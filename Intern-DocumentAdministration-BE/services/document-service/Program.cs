using DocumentService;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// --- Default Port 5002 for DocumentService ---
if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")))
{
    builder.WebHost.UseUrls("http://+:8080");
}

// --- DbContext: Hỗ trợ linh hoạt cả SQLite (Local Dev) và SQL Server (Docker / Team / Production) ---
var connStr = builder.Configuration.GetConnectionString("Default");
builder.Services.AddDbContext<DocumentDbContext>(options =>
{
    if (!string.IsNullOrEmpty(connStr) && (connStr.Contains(".db") || (connStr.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase) && connStr.EndsWith(".db", StringComparison.OrdinalIgnoreCase))))
    {
        options.UseSqlite(connStr);
    }
    else if (!string.IsNullOrEmpty(connStr))
    {
        options.UseSqlServer(connStr, sqlOptions =>
            sqlOptions.EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), null));
    }
    else
    {
        options.UseSqlite("Data Source=document_local.db");
    }
});

// --- JWT Authentication ---
var jwtSecret = builder.Configuration["Jwt:Secret"] ?? "REPLACE_WITH_RANDOM_STRING_AT_LEAST_32_CHARACTERS_LONG";
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
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// --- Typed Inter-Service HttpClients ---
builder.Services.AddHttpClient<IPartnerServiceClient, PartnerServiceClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:PartnerService"] ?? "http://localhost:5003");
});
builder.Services.AddHttpClient<IFilesServiceClient, FilesServiceClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:FilesService"] ?? "http://localhost:5004");
});
builder.Services.AddHttpClient<INotificationServiceClient, NotificationServiceClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:NotificationService"] ?? "http://localhost:5005");
});

// --- Global Exception Handler & Core Services ---
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddScoped<IDocumentBusinessService, DocumentBusinessService>();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Ensure DB is created for local testing
using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<DocumentDbContext>();
        db.Database.EnsureCreated();

        if (!await db.Documents.AnyAsync())
        {
            var seedPath = Path.Combine(AppContext.BaseDirectory, "documents_seed.json");
            if (!File.Exists(seedPath))
                seedPath = Path.Combine(builder.Environment.ContentRootPath, "documents_seed.json");

            if (File.Exists(seedPath))
            {
                var json = await File.ReadAllTextAsync(seedPath);
                using var docObj = System.Text.Json.JsonDocument.Parse(json);
                var root = docObj.RootElement;
                
                if (root.TryGetProperty("counters", out var countersElem))
                {
                    var counters = System.Text.Json.JsonSerializer.Deserialize<List<DocumentNumberCounter>>(countersElem.GetRawText(), new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (counters != null && counters.Count > 0)
                        db.DocumentNumberCounters.AddRange(counters);
                }

                if (root.TryGetProperty("documents", out var docsElem))
                {
                    var docs = System.Text.Json.JsonSerializer.Deserialize<List<Document>>(docsElem.GetRawText(), new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (docs != null && docs.Count > 0)
                        db.Documents.AddRange(docs);
                }

                await db.SaveChangesAsync();

                if (root.TryGetProperty("attachments", out var attElem))
                {
                    var atts = System.Text.Json.JsonSerializer.Deserialize<List<DocumentAttachment>>(attElem.GetRawText(), new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (atts != null && atts.Count > 0)
                        db.DocumentAttachments.AddRange(atts);
                }

                if (root.TryGetProperty("history", out var histElem))
                {
                    var hist = System.Text.Json.JsonSerializer.Deserialize<List<DocumentStatusHistory>>(histElem.GetRawText(), new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (hist != null && hist.Count > 0)
                        db.DocumentStatusHistory.AddRange(hist);
                }

                if (root.TryGetProperty("access", out var accessElem))
                {
                    var acc = System.Text.Json.JsonSerializer.Deserialize<List<DocumentDepartmentAccess>>(accessElem.GetRawText(), new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (acc != null && acc.Count > 0)
                        db.DocumentDepartmentAccess.AddRange(acc);
                }

                await db.SaveChangesAsync();
            }
        }
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Could not auto-seed documents.");
    }
}

app.UseExceptionHandler();

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "document-service" }));

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
