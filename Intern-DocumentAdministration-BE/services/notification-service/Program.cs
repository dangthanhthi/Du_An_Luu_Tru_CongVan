using Microsoft.EntityFrameworkCore;
using NotificationService.Data;
using NotificationService.Hubs;
using NotificationService.Services;

var builder = WebApplication.CreateBuilder(args);

// 1. Đăng ký các dịch vụ cơ bản
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 2. Đăng ký SignalR cho Real-time WebSockets Push
builder.Services.AddSignalR();

// 3. Đăng ký Database: Hỗ trợ linh hoạt cả SQLite (Local Dev) và SQL Server (Docker / Team / Production)
builder.Services.AddDbContext<NotificationDbContext>(options =>
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
        options.UseSqlite("Data Source=notification_local.db");
    }
});

// 4. Đăng ký Email Service
builder.Services.AddScoped<IEmailService, EmailService>();

// 5. Đăng ký In-Memory Non-Blocking Notification Queue & Background Worker
builder.Services.AddSingleton<INotificationQueue, InMemoryNotificationQueue>();
builder.Services.AddHostedService<NotificationBackgroundWorker>();

// 6. Cấu hình CORS cho Frontend kết nối SignalR & REST API
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.SetIsOriginAllowed(_ => true)
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
    db.Database.EnsureCreated();
}

app.UseCors();

// 7. BẬT GIAO DIỆN SWAGGER
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Notification API v1");
    c.RoutePrefix = "swagger";
});

// 8. Định tuyến Controllers & SignalR Hub
app.MapControllers();
app.MapHub<NotificationHub>("/hubs/notifications");

app.Run();