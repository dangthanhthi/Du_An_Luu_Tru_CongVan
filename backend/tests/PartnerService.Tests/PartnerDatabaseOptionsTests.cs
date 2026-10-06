using Microsoft.Extensions.Configuration;
using Xunit;

namespace PartnerService.Tests;

public sealed class PartnerDatabaseOptionsTests
{
    [Theory]
    [InlineData("Database:Provider", null)] [InlineData("Database:Provider", "unknown")]
    [InlineData("ConnectionStrings:Default", null)] [InlineData("ConnectionStrings:Default", "Data Source=local.db")]
    [InlineData("Database:InitializeOnStartup", "true")] [InlineData("Database:InitializeOnStartup", "invalid")]
    [InlineData("Database:SeedExamples", "true")] [InlineData("Database:SeedExamples", "invalid")]
    public void Production_rejects_missing_malformed_or_automatic_write_configuration(string name, string? value)
    {
        var values = new Dictionary<string, string?> { ["Database:Provider"] = "SqlServer", ["ConnectionStrings:Default"] = "Server=127.0.0.1,9;Database=synthetic;Integrated Security=true", ["Database:InitializeOnStartup"] = "false", ["Database:SeedExamples"] = "false" };
        values[name] = value;
        var error = Assert.Throws<InvalidOperationException>(() => PartnerDatabaseOptions.Read(new ConfigurationBuilder().AddInMemoryCollection(values).Build(), false));
        Assert.DoesNotContain("Server=", error.Message); Assert.DoesNotContain("Integrated Security", error.Message);
    }

    [Fact]
    public void Explicit_production_SQL_configuration_is_parsed_without_connecting()
    {
        var values = new Dictionary<string, string?> { ["Database:Provider"] = "SqlServer", ["ConnectionStrings:Default"] = "Server=127.0.0.1,9;Database=synthetic;Integrated Security=true" };
        var options = PartnerDatabaseOptions.Read(new ConfigurationBuilder().AddInMemoryCollection(values).Build(), false);
        Assert.Equal("SqlServer", options.Provider); Assert.False(options.Initialize); Assert.False(options.SeedExamples);
    }

    [Fact]
    public void Development_fallback_stays_local_and_requires_explicit_initialization()
    {
        var options = PartnerDatabaseOptions.Read(new ConfigurationBuilder().Build(), true);
        Assert.Equal("Sqlite", options.Provider); Assert.Equal("Data Source=partner_local.db", options.ConnectionString);
        Assert.False(options.Initialize); Assert.False(options.SeedExamples);
    }
}
