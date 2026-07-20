using Microsoft.EntityFrameworkCore;
using DAS.Api.Data;
using DAS.Api.Services;
using DAS.Api.GraphQL;
using DAS.Api.Models;

var builder = WebApplication.CreateBuilder(args);

// Enable CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Configure EF Core with SQLite
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=das.db"));

// Register Services
builder.Services.AddSingleton<JwtService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IDocumentService, DocumentService>();
builder.Services.AddScoped<IPartnerService, PartnerService>();
builder.Services.AddScoped<IDepartmentService, DepartmentService>();
builder.Services.AddScoped<INotificationService, NotificationService>();

// Configure GraphQL using HotChocolate
builder.Services
    .AddGraphQLServer()
    .AddQueryType<Queries>()
    .AddMutationType<Mutations>()
    .ModifyRequestOptions(opt => opt.ExecutionTimeout = TimeSpan.FromMinutes(2));

var app = builder.Build();

app.UseCors();

// Auto-migrate & seed database on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();

    // Seed Departments
    if (!db.Departments.Any())
    {
        var gd = new Department { Name = "Ban Giám đốc", Code = "BGD" };
        var hc = new Department { Name = "Phòng Hành chính", Code = "PHC" };
        var kt = new Department { Name = "Phòng Kế toán", Code = "PKT" };
        var tech = new Department { Name = "Phòng Kỹ thuật", Code = "PKH" };
        var kd = new Department { Name = "Phòng Kinh doanh", Code = "PKD" };

        db.Departments.AddRange(gd, hc, kt, tech, kd);
        db.SaveChanges();

        // Seed Users
        var userService = scope.ServiceProvider.GetRequiredService<IUserService>();
        userService.RegisterUserAsync("admin@das.com", "Adminstrator", "admin123", Role.Admin, null).Wait();
        userService.RegisterUserAsync("director@das.com", "Nguyễn Văn Director", "director123", Role.Director, gd.Id).Wait();
        userService.RegisterUserAsync("secretary@das.com", "Lê Thị Secretary", "secretary123", Role.DirectorSecretary, gd.Id).Wait();
        userService.RegisterUserAsync("employee@das.com", "Trần Văn Employee", "employee123", Role.Employee, hc.Id).Wait();

        // Seed Partners
        var p1 = new ExternalEntity { FullName = "Tập đoàn Công nghệ Viễn thông VNPT", ShortName = "VNPT", TaxCode = "0100684398", Address = "Hà Nội", ContactPerson = "Nguyễn Văn A", Phone = "0243789789", Email = "contact@vnpt.vn", Type = PartnerType.Business };
        var p2 = new ExternalEntity { FullName = "Cục Kiểm soát Thủ tục Hành chính", ShortName = "KSTTHC", TaxCode = "0102030405", Address = "Hà Nội", ContactPerson = "Trần Thị B", Phone = "0243654321", Email = "kstthc@chinhphu.vn", Type = PartnerType.Government };
        var p3 = new ExternalEntity { FullName = "Ngân hàng Thương mại Cổ phần Ngoại thương Việt Nam", ShortName = "Vietcombank", TaxCode = "0100112437", Address = "Hà Nội", ContactPerson = "Phạm Văn C", Phone = "0243934313", Email = "contact@vietcombank.com.vn", Type = PartnerType.Business };

        db.ExternalEntities.AddRange(p1, p2, p3);
        db.SaveChanges();
    }
}

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
                    $"Quét thực tế từ email ({request.EmailAccount}).\nNgười gửi: {senderDisplay}\nTiêu đề: {subject}\nNgày nhận: {message.Date.LocalDateTime}\nNội dung thư: {message.TextBody ?? ""}",
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
