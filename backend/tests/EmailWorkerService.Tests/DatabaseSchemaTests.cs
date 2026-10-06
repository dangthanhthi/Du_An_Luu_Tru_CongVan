using EmailWorkerService.Data;
using EmailWorkerService.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace EmailWorkerService.Tests;

public sealed class DatabaseSchemaTests
{
    [Fact]
    public void Sql_schema_has_a_baseline_snapshot_and_idempotent_provisioning_script()
    {
        // Generating SQL must work without a reachable database or application startup.
        using var db = new EmailWorkerDbContext(new DbContextOptionsBuilder<EmailWorkerDbContext>()
            .UseSqlServer("Server=127.0.0.1,1;Database=DesignOnly;Integrated Security=true;Connect Timeout=1").Options);
        Assert.NotEmpty(db.Database.GetMigrations());
        Assert.NotNull(db.GetService<IMigrationsAssembly>().ModelSnapshot);
        Assert.False(db.Database.HasPendingModelChanges());
        var script = db.GetService<IMigrator>().GenerateScript(options: MigrationsSqlGenerationOptions.Idempotent);
        foreach (var table in new[] { "EmailImapSettings", "EmailScanLogs", "EmailScanItemLogs" })
            Assert.Contains("CREATE TABLE [emailworker].[" + table + "]", script);
        Assert.Contains("__EFMigrationsHistory", script);
        Assert.Contains("IF NOT EXISTS", script);
        Assert.Contains("ON DELETE CASCADE", script);
        Assert.Contains("CREATE INDEX [IX_EmailScanItemLogs_ScanLogId]", script);
        Assert.DoesNotContain("INSERT INTO [emailworker]", script);
    }

    [Fact]
    public async Task Startup_rejects_model_drift_before_attempting_a_database_connection()
    {
        await using var db = new EmailWorkerDbContext(new DbContextOptionsBuilder<EmailWorkerDbContext>()
            .UseSqlServer("Server=127.0.0.1,1;Database=DesignOnly;Integrated Security=true;Connect Timeout=1")
            .ReplaceService<IModelCustomizer, DriftCustomizer>().Options);
        Assert.NotNull(db.GetService<IMigrationsAssembly>().ModelSnapshot);
        Assert.True(db.Database.HasPendingModelChanges());
        var settings = new EmailWorkerDatabaseOptions("SqlServer", db.Database.GetConnectionString()!, false);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => settings.VerifyAsync(db, development: false));
        Assert.Contains("differs from its migration snapshot", error.Message);
    }

    private sealed class DriftCustomizer(ModelCustomizerDependencies dependencies) : RelationalModelCustomizer(dependencies)
    {
        public override void Customize(ModelBuilder modelBuilder, DbContext context)
        {
            base.Customize(modelBuilder, context);
            modelBuilder.Entity<EmailScanLog>().Property<string>("UnmigratedFixtureField").HasMaxLength(10);
        }
    }
}
