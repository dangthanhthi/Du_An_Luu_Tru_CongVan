using DocumentService;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// --- Default Port 5002 for DocumentService ---
if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")) &&
    string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPNETCORE_HTTP_PORTS")))
{
    builder.WebHost.UseUrls("http://localhost:5002");
}

// --- DbContext ---
var connStr = builder.Configuration.GetConnectionString("Default");
builder.Services.AddDbContext<DocumentDbContext>(options =>
{
    if (!string.IsNullOrEmpty(connStr) && connStr.Contains("Server=sqlserver"))
    {
        options.UseSqlServer(connStr);
    }
    else
    {
        options.UseSqlite("Data Source=document_local.db");
    }
});

// --- JWT Authentication ---
var jwtSecret = builder.Configuration["Jwt:Secret"] ?? "REPLACE_WITH_RANDOM_STRING_AT_LEAST_32_CHARACTERS_LONG";
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
builder.Services.AddCors(options => options.AddDefaultPolicy(p => p.SetIsOriginAllowed(_ => true).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));
builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<ForwardUserTokenHandler>();

// --- Typed Inter-Service HttpClients ---
builder.Services.AddHttpClient<IPartnerServiceClient, PartnerServiceClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:PartnerService"] ?? "http://localhost:5003");
}).AddHttpMessageHandler<ForwardUserTokenHandler>();
builder.Services.AddHttpClient<IFilesServiceClient, FilesServiceClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:FilesService"] ?? "http://localhost:5004");
}).AddHttpMessageHandler<ForwardUserTokenHandler>();
builder.Services.AddHttpClient<INotificationServiceClient, NotificationServiceClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:NotificationService"] ?? "http://localhost:5007");
});

// --- Global Exception Handler & Core Services ---
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddScoped<IDocumentBusinessService, DocumentBusinessService>();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Apply owned-schema migrations in SQL Server; keep SQLite convenient for local tests.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DocumentDbContext>();
    if (db.Database.IsSqlServer())
        await db.Database.MigrateAsync();
    else
        db.Database.EnsureCreated();
}

app.UseExceptionHandler();

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "document-service" }));

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

