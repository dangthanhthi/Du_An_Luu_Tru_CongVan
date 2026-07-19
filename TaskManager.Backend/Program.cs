using Microsoft.EntityFrameworkCore;
using TaskManager.Backend.Data;
using TaskManager.Backend.Services;
using TaskManager.Backend.GraphQL;

var builder = WebApplication.CreateBuilder(args);

// Configure EF Core with SQLite
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=taskmanager.db"));

// Register Services
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ILDAPService, LDAPService>();

// Configure GraphQL using HotChocolate
builder.Services
    .AddGraphQLServer()
    .AddQueryType<UserQueries>()
    .AddMutationType<UserMutations>()
    .ModifyRequestOptions(opt => opt.ExecutionTimeout = TimeSpan.FromMinutes(2));

var app = builder.Build();

// Auto-migrate / create database on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

app.MapGraphQL();

app.Run();
