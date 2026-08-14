using FilesService.Data;
using FilesService.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 1. Đăng ký DbContext kết nối với SQL Server hoặc SQLite
var connStr = builder.Configuration.GetConnectionString("Default");
builder.Services.AddDbContext<FileDbContext>(options =>
{
    if (!string.IsNullOrEmpty(connStr) && connStr.Contains("Data Source=") && connStr.EndsWith(".db"))
    {
        options.UseSqlite(connStr);
    }
    else
    {
        options.UseSqlServer(connStr);
    }
    options.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
});

// 2. Đăng ký Interface và Implementation cho File Storage Service
builder.Services.AddScoped<IFileStorageService, FileStorageService>();

// 3. CẤU HÌNH BẢO MẬT JWT (BẮT BUỘC)
var jwtSecret = builder.Configuration["Jwt:Secret"];
if (string.IsNullOrEmpty(jwtSecret))
{
    // Đảm bảo không bị crash nếu chạy local chưa có key
    jwtSecret = "mot-chuoi-khoa-bi-mat-dai-hon-32-ky-tu-de-test-local-123";
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateIssuer = false, // Tạm tắt kiểm tra Issuer/Audience để test dễ dàng hơn
            ValidateAudience = false
        };
    });

var app = builder.Build();

// Tự động chạy Migration để sinh database SQLite/SQL Server lúc khởi động
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<FileDbContext>();
    if (db.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite")
    {
        db.Database.EnsureCreated();
    }
    else
    {
        db.Database.Migrate();
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Tạm comment dòng HttpsRedirection vì chạy trong Docker gọi nội bộ thường dùng HTTP (cổng 8080)
// app.UseHttpsRedirection(); 

// 4. BẮT BUỘC PHẢI CÓ UseAuthentication TRƯỚC UseAuthorization
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
// --- API SINH TOKEN NỘI BỘ DÀNH CHO DEV ---
app.MapGet("/api/dev/token", (IConfiguration config) =>
{
    var secret = config["Jwt:Secret"] ?? "mot-chuoi-khoa-bi-mat-dai-hon-32-ky-tu-de-test-local-123";
    var key = System.Text.Encoding.UTF8.GetBytes(secret);

    var tokenDescriptor = new Microsoft.IdentityModel.Tokens.SecurityTokenDescriptor
    {
        Subject = new System.Security.Claims.ClaimsIdentity(new[]
        {
            // Tự động sinh một UserId giả lập cho email-worker
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())
        }),
        Expires = DateTime.UtcNow.AddYears(1), // Token sống 1 năm để test thoải mái
        SigningCredentials = new Microsoft.IdentityModel.Tokens.SigningCredentials(
            new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(key),
            Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256Signature)
    };

    var tokenHandler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
    var token = tokenHandler.CreateToken(tokenDescriptor);

    return Results.Ok(new { serviceToken = tokenHandler.WriteToken(token) });
});
app.Run();