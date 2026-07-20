using Microsoft.EntityFrameworkCore;
using TaskManager.Backend.Data;
using TaskManager.Backend.Services;
using TaskManager.Backend.GraphQL;

var builder = WebApplication.CreateBuilder(args);

// Enable CORS services
builder.Services.AddCors();

// Configure EF Core with SQLite
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=taskmanager.db"));

// Register Services
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ILDAPService, LDAPService>();

// Configure GraphQL using HotChocolate
builder.Services
    .AddGraphQLServer()
    .AddQueryType<UserQueries>()
    .AddMutationType<UserMutations>()
    .ModifyRequestOptions(opt => opt.ExecutionTimeout = TimeSpan.FromMinutes(2));

var app = builder.Build();

// Auto-migrate / create database on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

// Enable CORS
app.UseCors(policy => policy.WithOrigins("http://localhost:3000")
                             .AllowAnyHeader()
                             .AllowAnyMethod()
                             .AllowCredentials());

app.MapGraphQL();

app.MapPost("/api/scan-email", async (ScanEmailRequest request) =>
{
    try
    {
        using var client = new MailKit.Net.Imap.ImapClient();
        client.ServerCertificateValidationCallback = (s, c, h, e) => true;
        
        await client.ConnectAsync(request.ImapServer, request.ImapPort, request.UseSsl);
        await client.AuthenticateAsync(request.EmailAccount, request.AppPassword);
        
        var inbox = client.Inbox;
        await inbox.OpenAsync(MailKit.FolderAccess.ReadOnly);
        
        int count = inbox.Count;
        int startIndex = Math.Max(0, count - 15);
        var uids = await inbox.SearchAsync(MailKit.Search.SearchQuery.All);
        var uidsToFetch = uids.Skip(startIndex).ToList();
        
        var scannedDocs = new List<ScannedDocument>();
        
        foreach (var uid in uidsToFetch.OrderByDescending(u => u.Id))
        {
            var message = await inbox.GetMessageAsync(uid);
            
            var pdfAttachment = message.Attachments
                .OfType<MimeKit.MimePart>()
                .FirstOrDefault(a => a.ContentType.MimeType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase) || 
                                     a.FileName?.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) == true);
                                     
            if (pdfAttachment != null)
            {
                string subject = message.Subject ?? "Không có chủ đề";
                string senderName = message.From.Mailboxes.FirstOrDefault()?.Name ?? "";
                string senderEmail = message.From.Mailboxes.FirstOrDefault()?.Address ?? "";
                string senderDisplay = string.IsNullOrEmpty(senderName) ? senderEmail : $"{senderName} ({senderEmail})";
                
                string originalNo = "";
                var match = System.Text.RegularExpressions.Regex.Match(subject, @"\d+/[A-Za-z0-9\-]+");
                if (match.Success)
                {
                    originalNo = match.Value;
                }
                else
                {
                    originalNo = "GMAIL-" + uid.Id.ToString();
                }
                
                string newDocId = $"doc-scan-real-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}-{uid.Id}";
                string docNo = $"CV-DEN-2026-{Random.Shared.Next(10000, 99999)}";
                
                scannedDocs.Add(new ScannedDocument(
                    newDocId,
                    docNo,
                    subject,
                    string.IsNullOrEmpty(senderName) ? senderEmail : senderName,
                    originalNo,
                    message.Date.LocalDateTime.ToString("dd/MM/yyyy"),
                    subject.Contains("Mật", StringComparison.OrdinalIgnoreCase) ? "Mật" : "Thường",
                    "Chờ xử lý",
                    message.TextBody ?? "",
                    pdfAttachment.FileName ?? "document.pdf"
                ));
            }
        }
        
        await client.DisconnectAsync(true);
        
        return Results.Ok(new { success = true, documents = scannedDocs });
    }
    catch (Exception ex)
    {
        return Results.Ok(new { success = false, error = ex.Message });
    }
});

app.Run();

public record ScanEmailRequest(
    string ImapServer,
    int ImapPort,
    bool UseSsl,
    string EmailAccount,
    string AppPassword
);

public record ScannedDocument(
    string Id,
    string DocNo,
    string Subject,
    string Sender,
    string OriginalNo,
    string Date,
    string Priority,
    string Status,
    string Content,
    string FileName
);
