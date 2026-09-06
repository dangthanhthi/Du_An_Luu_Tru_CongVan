using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .SetBasePath(builder.Environment.ContentRootPath)
    .AddJsonFile("ocelot.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"ocelot.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

var jwtSecret = builder.Configuration["Jwt:Secret"];
if (string.IsNullOrWhiteSpace(jwtSecret) || Encoding.UTF8.GetByteCount(jwtSecret) < 32)
    throw new InvalidOperationException("Jwt:Secret must be configured with at least 32 bytes.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
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
                await context.Response.WriteAsJsonAsync(new
                {
                    success = false, data = (object?)null,
                    message = "Authentication is required or the token is invalid.", errors = Array.Empty<string>()
                });
            },
            OnForbidden = context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return context.Response.WriteAsJsonAsync(new
                {
                    success = false, data = (object?)null,
                    message = "You do not have permission to perform this action.", errors = Array.Empty<string>()
                });
            }
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddOcelot(builder.Configuration);

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddPolicy("Frontend", policy =>
{
    if (allowedOrigins.Length == 0 && builder.Environment.IsDevelopment())
        policy.AllowAnyOrigin();
    else if (allowedOrigins.Length > 0)
        policy.WithOrigins(allowedOrigins);
    policy.AllowAnyMethod().AllowAnyHeader();
}));

var app = builder.Build();

app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();

app.Map("/health", health => health.Run(async context =>
{
    if (!HttpMethods.IsGet(context.Request.Method))
    {
        context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
        return;
    }

    await context.Response.WriteAsJsonAsync(new
    {
        success = true,
        data = new { status = "healthy", service = "gateway" },
        message = (string?)null,
        errors = Array.Empty<string>()
    });
}));

app.Use(async (context, next) =>
{
    await next();
    if (context.Response.HasStarted ||
        context.Response.StatusCode is not (StatusCodes.Status401Unauthorized or StatusCodes.Status403Forbidden) ||
        context.Response.ContentLength is > 0)
        return;

    context.Response.ContentLength = null;
    var message = context.Response.StatusCode == StatusCodes.Status401Unauthorized
        ? "Authentication is required or the token is invalid."
        : "You do not have permission to perform this action.";
    await context.Response.WriteAsJsonAsync(new
    {
        success = false,
        data = (object?)null,
        message,
        errors = Array.Empty<string>()
    });
});

await app.UseOcelot();
app.Run();
