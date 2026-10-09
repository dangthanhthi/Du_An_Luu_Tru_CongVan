using PartnerService;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

var jwtSecret = builder.Configuration["Jwt:Secret"];
if (string.IsNullOrWhiteSpace(jwtSecret) || Encoding.UTF8.GetByteCount(jwtSecret) < 32 ||
    jwtSecret == "REPLACE_WITH_RANDOM_STRING_AT_LEAST_32_CHARACTERS_LONG")
    throw new InvalidOperationException("Jwt:Secret must be provisioned with at least 32 UTF8 bytes and must not use the default placeholder.");
var database = PartnerDatabaseOptions.Read(builder.Configuration, builder.Environment.IsDevelopment());
builder.Services.AddDbContext<PartnerDbContext>(options =>
{
    if (database.Provider == "Sqlite")
    {
        options.UseSqlite(database.ConnectionString);
    }
    else
    {
        options.UseSqlServer(database.ConnectionString, sqlOptions =>
            sqlOptions.EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), null));
    }
});


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

builder.Services.AddAuthorization(options => options.AddPolicy("CatalogManage", policy =>
    policy.RequireAuthenticatedUser().RequireClaim("das_capability", "CatalogManage")));
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
{
    if (allowedOrigins.Length > 0) policy.WithOrigins(allowedOrigins);
    else if (builder.Environment.IsDevelopment()) policy.SetIsOriginAllowed(_ => true);
    policy.AllowAnyHeader().AllowAnyMethod().AllowCredentials();
}));

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// --- Business Services ---
builder.Services.AddScoped<IPartnerBusinessService, PartnerBusinessService>();
builder.Services.AddScoped<PartnerAuditQuery>();

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

// Migration/seed are explicit development operations, never an automatic customer rollout.
if (database.Initialize)
{
    if (!app.Environment.IsDevelopment()) throw new InvalidOperationException("Partner initialization is restricted to Development; deploy reviewed migrations separately.");
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<PartnerDbContext>();
    if (db.Database.IsSqlServer())
        await db.Database.MigrateAsync();
    else
        db.Database.EnsureCreated();

    if (database.SeedExamples && !await db.Partners.IgnoreQueryFilters().AnyAsync())
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
                    foreach (var item in items)
                    {
                        item.ShortName = string.IsNullOrWhiteSpace(item.ShortName) ? null : item.ShortName.Trim();
                        item.TaxCode = string.IsNullOrWhiteSpace(item.TaxCode) ? null : item.TaxCode.Trim();
                        item.NormalizedShortName = item.ShortName?.ToUpperInvariant();
                        item.NormalizedTaxCode = item.TaxCode?.ToUpperInvariant();
                    }
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
else if (database.Provider == "SqlServer")
{
    using var scope = app.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<PartnerDbContext>();
    if (!await db.Database.CanConnectAsync() || (await db.Database.GetPendingMigrationsAsync()).Any())
        throw new InvalidOperationException("Partner Database is unavailable or migrations are pending. Apply reviewed migrations separately before startup.");
    await db.Partners.IgnoreQueryFilters().AsNoTracking().Take(1).Select(x => x.Id).ToListAsync();
    await db.PartnerAudits.AsNoTracking().Take(1).Select(x => x.Id).ToListAsync();
}

app.UseExceptionHandler();

app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "partner-service" }));

app.UseCors();
app.Use(async (context, next) => {
    if (context.Request.Path.StartsWithSegments("/api"))
        context.Response.OnStarting(() => { context.Response.Headers.CacheControl = "no-store"; return Task.CompletedTask; });
    await next();
});
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
