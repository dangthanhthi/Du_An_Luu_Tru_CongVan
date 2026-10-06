using FilesService.Data;
using FilesService.Services;
using FilesService.Storage;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Das.PdfProtocol;

var builder=WebApplication.CreateBuilder(args);
var database=FileDatabaseOptions.Read(builder.Configuration,builder.Environment.IsDevelopment());
_ = PdfStorageOptions.Read(builder.Configuration);
builder.Services.AddControllers();builder.Services.AddEndpointsApiExplorer();builder.Services.AddSwaggerGen();
builder.Services.AddDbContext<FileDbContext>(options=> {
    if(database.Provider=="SqlServer")options.UseSqlServer(database.ConnectionString,sql=>sql.EnableRetryOnFailure(3));
    else options.UseSqlite(database.ConnectionString);
});
builder.Services.AddSingleton<IPdfThreatScanner,UnavailablePdfThreatScanner>();builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IFileStorageService,FileStorageService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient<IPdfDocumentClient,PdfDocumentHttpClient>().RedactLoggedHeaders(_=>true).ConfigurePrimaryHttpMessageHandler(()=>new HttpClientHandler { AllowAutoRedirect=false });
builder.Services.AddScoped<PdfClaims>();builder.Services.AddScoped<IPdfMaintenance,PdfMaintenance>();
builder.Services.AddScoped<PdfUploadMaintenance>();
if(PdfProtocolSettings.MaintenanceEnabled(builder.Configuration,builder.Environment,"Documents"))builder.Services.AddHostedService<PdfMaintenanceWorker>();
var jwtSecret=builder.Configuration["Jwt:Secret"];
if(string.IsNullOrWhiteSpace(jwtSecret) || Encoding.UTF8.GetByteCount(jwtSecret)<32)throw new InvalidOperationException("Jwt:Secret must be explicitly configured with at least 32 bytes.");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options=> {
    options.TokenValidationParameters=new() {
        ValidateIssuerSigningKey=true,IssuerSigningKey=new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
        ValidateIssuer=false,ValidateAudience=false,ValidateLifetime=true,ClockSkew=TimeSpan.FromSeconds(30)
    };
});
builder.Services.AddAuthorization();
var allowedOrigins=builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()??[];
builder.Services.AddCors(options=>options.AddDefaultPolicy(policy=> {
    if(allowedOrigins.Length>0)policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
}));
var app=builder.Build();
using(var scope=app.Services.CreateScope()) {
    var db=scope.ServiceProvider.GetRequiredService<FileDbContext>();
    if(database.Provider=="Sqlite")await FileSqliteUpgrade.ApplyAsync(db,database.Initialize);
    else if(database.Initialize)await db.Database.MigrateAsync();
    else if(!await db.Database.CanConnectAsync() || (await db.Database.GetPendingMigrationsAsync()).Any())
        throw new InvalidOperationException("Apply reviewed files SQL migrations before startup.");
}
if(app.Environment.IsDevelopment()){app.UseSwagger();app.UseSwaggerUI();}
app.UseCors();app.UseAuthentication();app.UseAuthorization();app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "files-service" }));
// No public service-token minting route; integration credentials are externally provisioned.
app.Run();
public partial class Program { }
