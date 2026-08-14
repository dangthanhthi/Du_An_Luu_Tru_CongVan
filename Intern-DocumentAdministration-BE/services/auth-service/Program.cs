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

builder.Services.AddDbContext<AuthDbContext>(options =>
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

builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
    if (db.Database.IsSqlServer())
        await db.Database.MigrateAsync();
    else
        db.Database.EnsureCreated();

    // Seed default roles and test users if empty
    if (!await db.Roles.AnyAsync())
    {
        var roleAdmin = new Role { Name = "Admin", Description = "Quản trị hệ thống" };
        var roleSec = new Role { Name = "Secretary", Description = "Thư ký" };
        var roleSecDir = new Role { Name = "SecretaryDirector", Description = "Thư ký BGD" };
        var roleEmp = new Role { Name = "Employee", Description = "Nhân viên" };
        db.Roles.AddRange(roleAdmin, roleSec, roleSecDir, roleEmp);
        await db.SaveChangesAsync();

        var pwdHash = BCrypt.Net.BCrypt.HashPassword("password");

        var userAdmin = new User { Username = "admin_user", FullName = "Quản trị viên Hệ thống", Email = "admin@company.com", PasswordHash = pwdHash, IsActive = true };
        var userSec = new User { Username = "secretary_user", FullName = "Thư ký Nguyễn Văn A", Email = "secretary@company.com", PasswordHash = pwdHash, IsActive = true };
        var userSecDir = new User { Username = "director_sec", FullName = "Thư ký BGD Trần Thị B", Email = "sec_director@company.com", PasswordHash = pwdHash, IsActive = true };
        var userEmp = new User { Username = "employee_user", FullName = "Nhân viên Lê Văn C", Email = "employee@company.com", PasswordHash = pwdHash, IsActive = true };

        db.Users.AddRange(userAdmin, userSec, userSecDir, userEmp);
        await db.SaveChangesAsync();

        db.UserRoles.Add(new UserRole { UserId = userAdmin.Id, RoleId = roleAdmin.Id });
        db.UserRoles.Add(new UserRole { UserId = userSec.Id, RoleId = roleSec.Id });
        db.UserRoles.Add(new UserRole { UserId = userSecDir.Id, RoleId = roleSecDir.Id });
        db.UserRoles.Add(new UserRole { UserId = userEmp.Id, RoleId = roleEmp.Id });
        await db.SaveChangesAsync();
    }
}

app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
