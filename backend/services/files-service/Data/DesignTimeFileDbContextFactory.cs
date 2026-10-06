using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
namespace FilesService.Data;
public sealed class DesignTimeFileDbContextFactory:IDesignTimeDbContextFactory<FileDbContext>
{
    public FileDbContext CreateDbContext(string[] args)=>new(new DbContextOptionsBuilder<FileDbContext>().UseSqlServer("Server=localhost;Database=DAS_File_DesignOnly;Integrated Security=true;TrustServerCertificate=true").Options);
}
