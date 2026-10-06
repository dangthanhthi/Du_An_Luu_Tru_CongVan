using DocumentService;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Das.PdfProtocol;

var builder = WebApplication.CreateBuilder(args);
var database = DatabaseStartupOptions.Read(builder.Configuration, builder.Environment.IsDevelopment());

// --- Default Port 5002 for DocumentService ---
if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")) &&
    string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPNETCORE_HTTP_PORTS")))
{
    builder.WebHost.UseUrls("http://localhost:5002");
}

// --- DbContext ---
builder.Services.AddDbContext<DocumentDbContext>(options =>
{
    if (database.Provider == "SqlServer")
    {
        options.UseSqlServer(database.ConnectionString, sql => sql.EnableRetryOnFailure(3));
    }
    else
    {
        options.UseSqlite(database.ConnectionString);
    }
});

// --- JWT Authentication ---
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
    });
builder.Services.AddAuthorization(options => {
    options.AddPolicy("CatalogManage", policy =>policy.RequireAuthenticatedUser().RequireClaim("das_capability", "CatalogManage"));
    options.AddPolicy("ReminderOperate", policy =>policy.RequireAuthenticatedUser().RequireClaim("das_capability", "ReminderOperate"));
});
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
{
    if (allowedOrigins.Length > 0) policy.WithOrigins(allowedOrigins);
    else if (builder.Environment.IsDevelopment()) policy.SetIsOriginAllowed(_ => true);
    policy.AllowAnyHeader().AllowAnyMethod().AllowCredentials();
}));
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
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHttpClient<IPdfFilesClient,PdfFilesHttpClient>().RedactLoggedHeaders(_=>true).ConfigurePrimaryHttpMessageHandler(()=>new HttpClientHandler { AllowAutoRedirect=false });
builder.Services.AddScoped<CurrentPdfService>();builder.Services.AddScoped<IPdfAuthority,DocumentV2PdfAuthority>();
builder.Services.AddScoped<IDocumentV2Authority,UnavailableDocumentV2Authority>();
builder.Services.AddScoped<V2RegistrationService>();builder.Services.AddScoped<V2DocumentEditor>();
builder.Services.AddScoped<V2DocumentLifecycle>();builder.Services.AddScoped<V2DocumentQueries>();
builder.Services.AddScoped<IPdfMaintenance,PdfMaintenance>();
if(PdfProtocolSettings.MaintenanceEnabled(builder.Configuration,builder.Environment,"Files"))builder.Services.AddHostedService<PdfMaintenanceWorker>();
builder.Services.AddScoped<CatalogService>();
builder.Services.AddScoped<IReportAuthority,UnavailableReportAuthority>();
builder.Services.AddScoped<CurrentPdfAvailability>();builder.Services.AddScoped<IncompleteReports>();
builder.Services.AddScoped<IReminderDirectory,UnavailableReminderDirectory>();builder.Services.AddScoped<IReminderTransport,DurableReminderTransport>();builder.Services.AddScoped<WeeklyReminders>();builder.Services.AddScoped<IReminderOperatorAuthority,UnavailableReminderOperatorAuthority>();
builder.Services.AddHttpClient<IReminderNotificationTransport,ConfiguredReminderNotificationTransport>().RedactLoggedHeaders(_=>true).ConfigurePrimaryHttpMessageHandler(()=>new HttpClientHandler{AllowAutoRedirect=false});
if(builder.Configuration.GetValue<bool>("Reminders:Enabled"))builder.Services.AddHostedService<WeeklyReminderWorker>();
builder.Services.AddScoped<IStaffAuthority,UnavailableStaffAuthority>();builder.Services.AddScoped<ITmsConnector,UnavailableTmsConnector>();builder.Services.AddScoped<MyStaffService>();
builder.Services.AddScoped<DocumentTasks>();
builder.Services.AddScoped<IDocumentNotificationAudience,UnavailableDocumentNotificationAudience>();
builder.Services.AddHttpClient<IDocumentNotificationTransport,ConfiguredNotificationTransport>().RedactLoggedHeaders(_=>true).ConfigurePrimaryHttpMessageHandler(()=>new HttpClientHandler{AllowAutoRedirect=false});builder.Services.AddScoped<DocumentNotifications>();
if(builder.Configuration.GetValue<bool>("Notifications:WorkerEnabled"))builder.Services.AddHostedService<DocumentNotificationWorker>();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Production startup checks the schema; operators apply SQL migrations separately.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DocumentDbContext>();
    if (db.Database.IsSqlServer())
    {
        if (database.Initialize) await db.Database.MigrateAsync();
        else if ((await db.Database.GetPendingMigrationsAsync()).Any())
            throw new InvalidOperationException("Apply document-service SQL migrations separately before starting this service.");
    }
    else
        await SqliteG1Upgrade.ApplyAsync(db, database.Initialize);
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
public partial class Program { }

