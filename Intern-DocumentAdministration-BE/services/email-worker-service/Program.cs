using EmailWorkerService.Services.Integration;
using EmailWorkerService;
using EmailWorkerService.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// 1. Đăng ký các service
builder.Services.AddScoped<IEmailProcessor, EmailProcessor>();
builder.Services.AddHttpClient<IFilesServiceClient, FilesServiceClient>();
builder.Services.AddHttpClient<IDocumentServiceClient, DocumentServiceClient>();
builder.Services.AddHostedService<EmailBackgroundWorker>();


// Cấu hình Minimal API và Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// 2. API Trigger Nội bộ dành cho Dev/Admin[cite: 1]
app.MapPost("/api/email-worker/trigger", async (IEmailProcessor processor) =>
{
    await processor.ProcessIncomingEmailsAsync();

    return Results.Ok(new
    {
        success = true,
        message = "Đã kích hoạt thành công tiến trình quét email thủ công.",
        data = (object?)null, // Thêm dấu ? vào đây
        errors = Array.Empty<string>()
    });
})
.WithName("TriggerEmailScan")
.WithOpenApi();


app.Run();