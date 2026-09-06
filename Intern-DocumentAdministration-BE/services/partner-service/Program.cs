using PartnerService;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

var connStr = builder.Configuration.GetConnectionString("Default");
builder.Services.AddDbContext<PartnerDbContext>(options =>
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
        options.UseSqlite("Data Source=partner_local.db");
    }
});


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

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// --- Business Services ---
builder.Services.AddScoped<IPartnerBusinessService, PartnerBusinessService>();

builder.Services.AddControllers();
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

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PartnerDbContext>();
    if (db.Database.IsSqlServer())
        await db.Database.MigrateAsync();
    else
        db.Database.EnsureCreated();

    if (!await db.Partners.AnyAsync())
    {
        var seedPath = Path.Combine(AppContext.BaseDirectory, "partners_seed.json");
        if (!File.Exists(seedPath))
            seedPath = Path.Combine(builder.Environment.ContentRootPath, "partners_seed.json");

        if (File.Exists(seedPath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(seedPath);
                var items = System.Text.Json.JsonSerializer.Deserialize<List<Partner>>(json, new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                if (items != null && items.Count > 0)
                {
                    db.Partners.AddRange(items);
                    await db.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                app.Logger.LogWarning(ex, "Could not auto-seed partners from seed file.");
            }
        }
    }
}

app.UseExceptionHandler();

app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "partner-service" }));

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
