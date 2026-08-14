using Ocelot.DependencyInjection;
using Ocelot.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Configure Ocelot
builder.Configuration.AddJsonFile("ocelot.json", optional: false, reloadOnChange: true);
builder.Services.AddOcelot();

// Enable CORS for frontend
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// Explicitly run on port 8080
builder.WebHost.UseUrls("http://localhost:8080");

var app = builder.Build();

app.UseCors("AllowAll");
app.UseRouting();

app.UseEndpoints(endpoints =>
{
    endpoints.MapGet("/health", () => Results.Ok(new
    {
        success = true,
        data = new
        {
            status = "healthy",
            service = "gateway"
        },
        message = (string?)null,
        errors = Array.Empty<string>()
    }));
});

await app.UseOcelot();

app.Run();
