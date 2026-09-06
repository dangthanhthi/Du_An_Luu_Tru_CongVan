using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using AuthService;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AuthDbContext>(options =>
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
        options.UseSqlite("Data Source=auth_local.db");
    }
});

var jwtSecret = builder.Configuration["Jwt:Secret"];
if (string.IsNullOrWhiteSpace(jwtSecret) || Encoding.UTF8.GetByteCount(jwtSecret) < 32)
    throw new InvalidOperationException("Jwt:Secret must be configured with at least 32 bytes.");
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
        options.Events = new JwtBearerEvents
        {
            OnChallenge = async context =>
            {
                context.HandleResponse();
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(ApiResponse<object>.Fail("Authentication is required or the token is invalid."));
            },
            OnForbidden = context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return context.Response.WriteAsJsonAsync(ApiResponse<object>.Fail("You do not have permission to perform this action."));
            }
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

builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddControllers();
builder.Services.Configure<Microsoft.AspNetCore.Mvc.ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var errors = context.ModelState
            .SelectMany(entry => entry.Value?.Errors.Select(error => $"{entry.Key}: {error.ErrorMessage}") ?? [])
            .ToArray();
        return new Microsoft.AspNetCore.Mvc.BadRequestObjectResult(
            ApiResponse<object>.Fail("Validation failed.", errors));
    };
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Nhập access token (không cần gõ chữ 'Bearer', Swagger tự thêm)."
    });
    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

app.UseExceptionHandler(handler => handler.Run(async context =>
{
    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
    await context.Response.WriteAsJsonAsync(ApiResponse<object>.Fail("An unexpected server error occurred."));
}));

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
    if (db.Database.IsSqlServer())
    {
        await db.Database.MigrateAsync();
    }
    else
    {
        db.Database.EnsureCreated();
    }

    // 1. Seed Roles
    if (!await db.Roles.AnyAsync())
    {
        var roleAdmin = new Role { Name = "Admin", Description = "Quản trị hệ thống" };
        var roleSec = new Role { Name = "Secretary", Description = "Thư ký" };
        var roleSecDir = new Role { Name = "SecretaryDirector", Description = "Thư ký Ban Giám đốc" };
        var roleEmp = new Role { Name = "Employee", Description = "Nhân viên" };
        db.Roles.AddRange(roleAdmin, roleSec, roleSecDir, roleEmp);
        await db.SaveChangesAsync();
    }

    // 2. Seed Departments
    if (!await db.Departments.AnyAsync())
    {
        var deptBgd = new Department { Name = "Ban Giám đốc", Code = "BGD", IsActive = true };
        var deptHc = new Department { Name = "Phòng Hành chính - Văn thư", Code = "HC-VT", IsActive = true };
        var deptKt = new Department { Name = "Phòng Kỹ thuật & Công nghệ", Code = "KT-CN", IsActive = true };
        var deptKh = new Department { Name = "Phòng Kế hoạch - Tài chính", Code = "KH-TC", IsActive = true };
        db.Departments.AddRange(deptBgd, deptHc, deptKt, deptKh);
        await db.SaveChangesAsync();
    }

    // 3. Seed Users
    if (!await db.Users.AnyAsync())
    {
        var adminRole = await db.Roles.FirstAsync(r => r.Name == "Admin");
        var secRole = await db.Roles.FirstAsync(r => r.Name == "Secretary");
        var empRole = await db.Roles.FirstAsync(r => r.Name == "Employee");
        var hcDept = await db.Departments.FirstAsync(d => d.Code == "HC-VT");

        var pwdHash = BCrypt.Net.BCrypt.HashPassword("password");

        var userAdmin = new User
        {
            Username = "admin_user",
            FullName = "Quản trị viên Hệ thống",
            Email = "admin@das.vn",
            PasswordHash = pwdHash,
            DepartmentId = hcDept.Id,
            IsActive = true
        };
        var userSec = new User
        {
            Username = "secretary_user",
            FullName = "Thư ký Nguyễn Văn A",
            Email = "secretary@das.vn",
            PasswordHash = pwdHash,
            DepartmentId = hcDept.Id,
            IsActive = true
        };
        var userEmp = new User
        {
            Username = "employee_user",
            FullName = "Nhân viên Lê Văn C",
            Email = "employee@das.vn",
            PasswordHash = pwdHash,
            DepartmentId = hcDept.Id,
            IsActive = true
        };

        db.Users.AddRange(userAdmin, userSec, userEmp);
        await db.SaveChangesAsync();

        db.UserRoles.Add(new UserRole { UserId = userAdmin.Id, RoleId = adminRole.Id });
        db.UserRoles.Add(new UserRole { UserId = userSec.Id, RoleId = secRole.Id });
        db.UserRoles.Add(new UserRole { UserId = userEmp.Id, RoleId = empRole.Id });
        await db.SaveChangesAsync();
    }
}

app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/health", () => Results.Ok(ApiResponse<object>.Ok(new { status = "healthy", service = "auth-service" })))
    .AllowAnonymous();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
