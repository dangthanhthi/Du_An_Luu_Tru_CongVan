using Microsoft.EntityFrameworkCore;
using NotificationService.Data;
using NotificationService.Hubs;
using NotificationService.Services;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// 1. Đăng ký các dịch vụ cơ bản
builder.Services.AddControllers().AddJsonOptions(o=>o.JsonSerializerOptions.UnmappedMemberHandling=System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 2. Đăng ký SignalR cho Real-time WebSockets Push
builder.Services.AddSignalR();
var secret=builder.Configuration["Jwt:Secret"];
if(string.IsNullOrWhiteSpace(secret)||Encoding.UTF8.GetByteCount(secret)<32)throw new InvalidOperationException("A provisioned JWT signing key is required.");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o=>o.TokenValidationParameters=new TokenValidationParameters{
    ValidateIssuerSigningKey=true,IssuerSigningKey=new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),ValidateLifetime=true,ValidateIssuer=false,ValidateAudience=false,ClockSkew=TimeSpan.FromSeconds(30)});
builder.Services.AddAuthorization(o=>{
    var validActor=new AuthorizationPolicyBuilder().RequireAuthenticatedUser().RequireAssertion(c=>Guid.TryParse(c.User.FindFirstValue(ClaimTypes.NameIdentifier)??c.User.FindFirstValue("sub"),out var id)&&id!=Guid.Empty).Build();
    o.DefaultPolicy=validActor;
    foreach(var capability in new[]{"NotificationSend","NotificationAudit"})o.AddPolicy(capability,p=>p.Combine(validActor).RequireClaim("das_capability",capability));
});

// 3. Đăng ký Database: Hỗ trợ linh hoạt cả SQLite (Local Dev) và SQL Server (Docker / Team / Production)
var database = NotificationDatabaseOptions.Read(builder.Configuration, builder.Environment.IsDevelopment());
builder.Services.AddDbContext<NotificationDbContext>(options =>
{
    if (database.Provider == "Sqlite")
    {
        options.UseSqlite(database.ConnectionString);
    }
    else
    {
        options.UseSqlServer(database.ConnectionString);
    }
});

// 4. Đăng ký Email Service

// 5. Đăng ký In-Memory Non-Blocking Notification Queue & Background Worker
builder.Services.AddSingleton(TimeProvider.System);builder.Services.AddScoped<DurableNotifications>();builder.Services.AddScoped<DurableDelivery>();builder.Services.AddScoped<IDeliveryEmail,ConfiguredDeliveryEmail>();
if(builder.Configuration.GetValue<bool>("Delivery:WorkerEnabled"))builder.Services.AddHostedService<DurableNotificationWorker>();

// 6. Cấu hình CORS cho Frontend kết nối SignalR & REST API
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>()??[])
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

var app = builder.Build();

// Tự động khởi tạo Database SQLite
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
    if(database.Initialize){
        if(!app.Environment.IsDevelopment())throw new InvalidOperationException("Automatic initialization is restricted to Development.");
        if (db.Database.IsSqlServer()) await db.Database.MigrateAsync();
        else await db.Database.EnsureCreatedAsync();
    }
    // Check both schema and the durable inbox. Legacy local DBs require an explicit upgrade.
    await db.Set<NotificationService.Models.DeliveryInbox>().AsNoTracking().Take(1).Select(x=>x.Id).ToListAsync();
    if(db.Database.IsSqlServer()&&(await db.Database.GetPendingMigrationsAsync()).Any())throw new InvalidOperationException("Apply notification SQL migrations separately before startup.");
}

app.UseCors();
app.UseAuthentication();app.UseAuthorization();
app.Use(async(context,next)=>{context.Response.Headers.CacheControl="no-store";await next();});

// 7. BẬT GIAO DIỆN SWAGGER
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Notification API v1");
    c.RoutePrefix = "swagger";
});

// 8. Định tuyến Controllers & SignalR Hub
app.MapControllers();
app.MapGet("/health",()=>Results.Ok(new{status="healthy",service="notification-service",workerEnabled=builder.Configuration.GetValue<bool>("Delivery:WorkerEnabled"),smtpEnabled=builder.Configuration.GetValue<bool>("Smtp:DeliveryEnabled")}));
app.MapHub<NotificationHub>("/hubs/notifications");

app.Run();
