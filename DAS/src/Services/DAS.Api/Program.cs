using Microsoft.EntityFrameworkCore;
using DAS.Api.Data;
using DAS.Api.Services;
using DAS.Api.GraphQL;
using DAS.Api.Models;

var builder = WebApplication.CreateBuilder(args);

// Enable CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Configure EF Core with SQLite
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=das.db"));

// Register Services
builder.Services.AddSingleton<JwtService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IDocumentService, DocumentService>();
builder.Services.AddScoped<IPartnerService, PartnerService>();
builder.Services.AddScoped<IDepartmentService, DepartmentService>();
builder.Services.AddScoped<INotificationService, NotificationService>();

// Configure GraphQL using HotChocolate
builder.Services
    .AddGraphQLServer()
    .AddQueryType<Queries>()
    .AddMutationType<Mutations>()
    .ModifyRequestOptions(opt => opt.ExecutionTimeout = TimeSpan.FromMinutes(2));

var app = builder.Build();

app.UseCors();

// Auto-migrate & seed database on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();

    // Seed Departments
    if (!db.Departments.Any())
    {
        var gd = new Department { Name = "Ban Giám đốc", Code = "BGD" };
        var hc = new Department { Name = "Phòng Hành chính", Code = "PHC" };
        var kt = new Department { Name = "Phòng Kế toán", Code = "PKT" };
        var tech = new Department { Name = "Phòng Kỹ thuật", Code = "PKH" };
        var kd = new Department { Name = "Phòng Kinh doanh", Code = "PKD" };

        db.Departments.AddRange(gd, hc, kt, tech, kd);
        db.SaveChanges();

        // Seed Users
        var userService = scope.ServiceProvider.GetRequiredService<IUserService>();
        userService.RegisterUserAsync("admin@das.com", "Adminstrator", "admin123", Role.Admin, null).Wait();
        userService.RegisterUserAsync("director@das.com", "Nguyễn Văn Director", "director123", Role.Director, gd.Id).Wait();
        userService.RegisterUserAsync("secretary@das.com", "Lê Thị Secretary", "secretary123", Role.DirectorSecretary, gd.Id).Wait();
        userService.RegisterUserAsync("employee@das.com", "Trần Văn Employee", "employee123", Role.Employee, hc.Id).Wait();

        // Seed Partners
        var p1 = new ExternalEntity { FullName = "Tập đoàn Công nghệ Viễn thông VNPT", ShortName = "VNPT", TaxCode = "0100684398", Address = "Hà Nội", ContactPerson = "Nguyễn Văn A", Phone = "0243789789", Email = "contact@vnpt.vn", Type = PartnerType.Business };
        var p2 = new ExternalEntity { FullName = "Cục Kiểm soát Thủ tục Hành chính", ShortName = "KSTTHC", TaxCode = "0102030405", Address = "Hà Nội", ContactPerson = "Trần Thị B", Phone = "0243654321", Email = "kstthc@chinhphu.vn", Type = PartnerType.Government };
        var p3 = new ExternalEntity { FullName = "Ngân hàng Thương mại Cổ phần Ngoại thương Việt Nam", ShortName = "Vietcombank", TaxCode = "0100112437", Address = "Hà Nội", ContactPerson = "Phạm Văn C", Phone = "0243934313", Email = "contact@vietcombank.com.vn", Type = PartnerType.Business };

        db.ExternalEntities.AddRange(p1, p2, p3);
        db.SaveChanges();
    }
}

app.MapGraphQL();

app.Run();
