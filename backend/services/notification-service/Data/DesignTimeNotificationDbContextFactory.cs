using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
namespace NotificationService.Data;
public sealed class DesignTimeNotificationDbContextFactory:IDesignTimeDbContextFactory<NotificationDbContext>
{
    public NotificationDbContext CreateDbContext(string[] args)=>new(new DbContextOptionsBuilder<NotificationDbContext>().UseSqlServer("Server=localhost;Database=NotificationDesignTime;Integrated Security=true;TrustServerCertificate=true").Options);
}
