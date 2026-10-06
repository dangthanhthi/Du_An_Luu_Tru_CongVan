using Microsoft.Extensions.Configuration;
using NotificationService.Data;
using Xunit;

namespace NotificationService.Tests;

public sealed class NotificationDatabaseOptionsTests
{
    [Theory]
    [InlineData("Database:Provider", null)] [InlineData("Database:Provider", "unknown")]
    [InlineData("ConnectionStrings:Default", null)] [InlineData("ConnectionStrings:Default", "Data Source=local.db")]
    [InlineData("Database:Initialize", "true")] [InlineData("Database:Initialize", "invalid")]
    public void Production_rejects_missing_malformed_or_automatic_write_configuration(string name, string? value)
    {
        var values = new Dictionary<string, string?> { ["Database:Provider"] = "SqlServer", ["ConnectionStrings:Default"] = "Server=127.0.0.1,9;Database=synthetic;Integrated Security=true", ["Database:Initialize"] = "false" };
        values[name] = value;
        var error = Assert.Throws<InvalidOperationException>(() => NotificationDatabaseOptions.Read(new ConfigurationBuilder().AddInMemoryCollection(values).Build(), false));
        Assert.DoesNotContain("Server=", error.Message); Assert.DoesNotContain("Integrated Security", error.Message);
    }

    [Fact]
    public void Explicit_production_SQL_configuration_is_parsed_without_connecting()
    {
        var values = new Dictionary<string, string?> { ["Database:Provider"] = "SqlServer", ["ConnectionStrings:Default"] = "Server=127.0.0.1,9;Database=synthetic;Integrated Security=true" };
        var options = NotificationDatabaseOptions.Read(new ConfigurationBuilder().AddInMemoryCollection(values).Build(), false);
        Assert.Equal("SqlServer", options.Provider); Assert.False(options.Initialize);
    }

    [Fact]
    public void Development_fallback_stays_local_and_requires_explicit_initialization()
    {
        var options = NotificationDatabaseOptions.Read(new ConfigurationBuilder().Build(), true);
        Assert.Equal("Sqlite", options.Provider); Assert.Equal("Data Source=notification_local.db", options.ConnectionString); Assert.False(options.Initialize);
    }
}
