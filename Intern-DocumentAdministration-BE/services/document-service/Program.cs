using DocumentService;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// --- DbContext ---
var connStr = builder.Configuration.GetConnectionString("Default");
if (string.IsNullOrEmpty(connStr))
{
    throw new InvalidOperationException(
        "Chuỗi kết nối database ConnectionStrings:Default không hợp lệ hoặc chưa được thiết lập.");
}

builder.Services.AddDbContext<DocumentDbContext>(options =>
{
    if (connStr.Contains("Data Source=") && connStr.EndsWith(".db"))
    {
        options.UseSqlite(connStr);
    }
    else
    {
        options.UseSqlServer(connStr, sqlOptions =>
            sqlOptions.EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), null));
    }
});


// --- JWT Authentication ---
var jwtSecret = builder.Configuration["Jwt:Secret"];
if (string.IsNullOrEmpty(jwtSecret) || jwtSecret == "REPLACE_WITH_RANDOM_STRING_AT_LEAST_32_CHARACTERS_LONG")
{
    throw new InvalidOperationException(
        "Cấu hình Jwt:Secret chưa được thiết lập hoặc đang sử dụng khóa bí mật mặc định không an toàn.");
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
builder.Services.AddHttpContextAccessor();

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
    client.BaseAddress = new Uri(builder.Configuration["Services:NotificationService"] ?? "http://localhost:5007");
});
builder.Services.AddHttpClient<IAuthServiceClient, AuthServiceClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:AuthService"] ?? "http://localhost:5001");
});

// --- Global Exception Handler & Core Services ---
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddScoped<IDocumentBusinessService, DocumentBusinessService>();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// --- Database Initialization ---
// SQL Server (Docker/Production): chạy migrations tự động — giữ __EFMigrationsHistory nhất quán
// SQLite (Local Dev): EnsureCreated vì không có migrations cho SQLite
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DocumentDbContext>();
    if (db.Database.IsSqlServer())
    {
        await db.Database.MigrateAsync();
    }
    else
    {
        db.Database.EnsureCreated();
    }
}

app.UseExceptionHandler();

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "document-service" }));

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
