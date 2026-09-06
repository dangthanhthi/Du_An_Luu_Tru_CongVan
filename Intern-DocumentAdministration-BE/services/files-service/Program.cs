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

// 1. Đăng ký DbContext kết nối với SQLite / SQL Server
builder.Services.AddDbContext<FileDbContext>(options =>
{
    var conn = builder.Configuration.GetConnectionString("Default");
    if (!string.IsNullOrEmpty(conn) && (conn.Contains(".db") || (conn.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase) && conn.EndsWith(".db", StringComparison.OrdinalIgnoreCase))))
    {
        options.UseSqlite(conn);
    }
    else if (!string.IsNullOrEmpty(conn))
    {
        options.UseSqlServer(conn, sqlOptions =>
            sqlOptions.EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), null));
    }
    else
    {
        options.UseSqlite("Data Source=files_local.db");
    }
});

// 2. Đăng ký Interface và Implementation cho File Storage Service
builder.Services.AddScoped<IFileStorageService, FileStorageService>();

// 3. CẤU HÌNH BẢO MẬT JWT (BẮT BUỘC)
var jwtSecret = builder.Configuration["Jwt:Secret"];
if (string.IsNullOrEmpty(jwtSecret))
{
    jwtSecret = "SuperSecretJwtSigningKeyForDocumentAdmin2026System!";
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateIssuer = false,
            ValidateAudience = false
        };
    });

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

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<FileDbContext>();
    db.Database.EnsureCreated();

    try
    {
        if (!await db.Files.AnyAsync())
        {
            var seedPath = Path.Combine(AppContext.BaseDirectory, "files_seed.json");
            if (!File.Exists(seedPath))
                seedPath = Path.Combine(builder.Environment.ContentRootPath, "files_seed.json");

            if (File.Exists(seedPath))
            {
                var json = await File.ReadAllTextAsync(seedPath);
                var items = System.Text.Json.JsonSerializer.Deserialize<List<FilesService.Models.Entities.FileRecord>>(json, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (items != null && items.Count > 0)
                {
                    db.Files.AddRange(items);
                    await db.SaveChangesAsync();
                }
            }
        }
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Could not auto-seed files.");
    }
}

// Configure the HTTP request pipeline.
app.UseSwagger();
app.UseSwaggerUI();

app.UseCors();
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