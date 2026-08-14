using PartnerService;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// --- DbContext ---
// SQL Server (Docker / Production) neu co ConnectionString hop le,
// SQLite cho local dev / CI khong co SQL Server
var connStr = builder.Configuration.GetConnectionString("Default");
if (string.IsNullOrEmpty(connStr))
{
    throw new InvalidOperationException(
        "Chuỗi kết nối database ConnectionStrings:Default không hợp lệ hoặc chưa được thiết lập.");
}

builder.Services.AddDbContext<PartnerDbContext>(options =>
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


// --- JWT Authentication (khop voi auth-service) ---
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
            ValidateIssuer   = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ClockSkew        = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddAuthorization();

// --- Exception Handler (tra JSON {success,message} nhat quan) ---
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// --- Business Services ---
builder.Services.AddScoped<IPartnerBusinessService, PartnerBusinessService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// --- Database Initialization ---
// Fix #7 pattern: phan biet SQL Server vs SQLite
// Khong boc try/catch - neu fail la service khong nen khoi dong
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PartnerDbContext>();
    if (db.Database.IsSqlServer())
        await db.Database.MigrateAsync();
    else
        db.Database.EnsureCreated();
}

app.UseExceptionHandler();

app.UseSwagger();
app.UseSwaggerUI();

// Health check endpoint (khop voi pattern cua auth-service)
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "partner-service" }));

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
