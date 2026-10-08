using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using DocumentService;
using Microsoft.Extensions.Configuration;

namespace DocumentService.Tests;

public class Tv3IntegrationTests
{
    // 1. Test Reminder Directory fetches from Auth Service
    [Fact]
    public async Task HttpReminderDirectory_CanFetchDepartments_FromAuthService()
    {
        // Arrange: Use the same symmetric key the auth service is running with
        var config = new ConfigurationBuilder().AddInMemoryCollection(new[]
        {
            new KeyValuePair<string, string?>("Jwt:Secret", "DAS_SECRET_KEY_FOR_LOCAL_DEV_AT_LEAST_32_BYTES_LONG")
        }).Build();

        var http = new HttpClient { BaseAddress = new Uri("http://localhost:5001") };
        var directory = new HttpReminderDirectory(http, config);

        // Act
        var departments = await directory.DepartmentsAsync(CancellationToken.None);

        // Assert: It shouldn't throw 401 Unauthorized, meaning our System JWT generation works!
        Assert.NotNull(departments);
        Assert.True(departments.Count >= 0);
    }

    // 2. Test TMS Connector builds requests properly
    [Fact]
    public async Task HttpTmsConnector_CreateAsync_ReturnsPendingOrLinked()
    {
        // Arrange
        var http = new HttpClient { BaseAddress = new Uri("http://localhost:8081") };
        var tms = new HttpTmsConnector(http);
        var correlationId = Guid.NewGuid();

        // Act
        // Because localhost:8081 is not running (Mock TMS), it should catch HttpRequestException and return PendingConfiguration
        var result = await tms.CreateAsync(correlationId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Test Task", CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("PendingConfiguration", result.State);
    }
}
