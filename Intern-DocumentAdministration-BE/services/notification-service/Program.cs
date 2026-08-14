using Microsoft.EntityFrameworkCore;
using NotificationService.Data;
using NotificationService.Services;

var builder = WebApplication.CreateBuilder(args);

// 1. Đăng ký các dịch vụ cơ bản
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(); // Kích hoạt bộ tạo Swagger

// 2. Đăng ký Database
builder.Services.AddDbContext<NotificationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

// 3. Đăng ký Email Service
builder.Services.AddScoped<IEmailService, EmailService>();

var app = builder.Build();

// 4. BẬT GIAO DIỆN SWAGGER (Luôn bật để test dễ dàng)
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Notification API v1");
    c.RoutePrefix = "swagger"; // Đảm bảo UI luôn ở đường dẫn /swagger
});

// 5. Khởi chạy Controller
app.MapControllers();

app.Run();