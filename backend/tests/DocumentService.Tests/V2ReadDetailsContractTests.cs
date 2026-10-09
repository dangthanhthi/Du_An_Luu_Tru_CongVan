using System.Text.Json;
using DocumentService;
using Xunit;

namespace DocumentService.Tests;

public sealed class V2ReadDetailsContractTests
{
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData("Incoming")]
    [InlineData("Outgoing")]
    [InlineData("Internal")]
    public async Task HTTP_detail_keeps_all_read_fields_and_separates_recipient_input_from_output(string kind)
    {
        var authority = new V2HttpTests.Authority();
        await using var host = new V2HttpTests.Host(authority);
        using var client = host.Client(authority.User);
        var result = await V2HttpTests.Register(client, V2HttpTests.Draft(authority, kind), "read-dto-http");
        var id = result.GetProperty("id").GetGuid();
        authority.Readable.Add(id);
        var detail = await V2HttpTests.Data(await client.GetAsync($"/api/v2/documents/{id}"));
        var fields = detail.GetProperty("details");
        Assert.Equal(id, fields.GetProperty("documentId").GetGuid());
        Assert.Equal(14, fields.EnumerateObject().Count());
        Assert.False(fields.TryGetProperty("document", out _));
        Assert.False(fields.TryGetProperty("recipientPartnerIds", out _));
        Assert.False(fields.TryGetProperty("distributionTargetIds", out _));
        if (kind == "Incoming") {
            Assert.Equal("2026-10-05", fields.GetProperty("receivingDate").GetString());
            Assert.Equal("Test external", fields.GetProperty("senderNameSnapshot").GetString());
            Assert.Equal("EMAIL", fields.GetProperty("methodCode").GetString());
        }
        Assert.Equal(kind == "Internal" ? 0 : 1, detail.GetProperty("recipients").GetArrayLength());
        authority.Readable.Clear();
        Assert.Equal(System.Net.HttpStatusCode.NotFound, (await client.GetAsync($"/api/v2/documents/{id}")).StatusCode);
    }

    [Fact]
    public void Detail_response_contract_does_not_expose_the_persistence_entity()
    {
        var property = typeof(V2DocumentDetail).GetProperty(nameof(V2DocumentDetail.Details))!;
        Assert.NotEqual(typeof(DocumentKindDetails), property.PropertyType);
        Assert.Equal("V2KindDetailsView", property.PropertyType.Name);
        Assert.Null(property.PropertyType.GetProperty("Document"));
    }

    [Theory]
    [InlineData("INCOMING")]
    [InlineData("OUTGOING")]
    [InlineData("INTERNAL")]
    public async Task Query_returns_a_detached_read_snapshot_and_preserves_the_existing_JSON(string kind)
    {
        await using var fixture = await V2EditingTests.Fixture.Create();
        var document = await fixture.Register(kind);
        var entity = new DocumentKindDetails {
            DocumentId = document.Id, ReceivingDate = new DateOnly(2026, 10, 9),
            SenderPartnerId = Guid.Parse("aaaaaaaa-0000-4000-8000-000000000001"),
            SenderNameSnapshot = "Cơ quan gửi", ReferenceNumber = "REF/2026/01",
            MethodCode = "EMAIL", MethodNameSnapshot = "eMail",
            DocumentTypeCode = "LETTER", DocumentTypeNameSnapshot = "Letter",
            CategoryCode = "TEST", CategoryNameSnapshot = "Phân loại kiểm thử",
            ContractNumber = "HĐ/2026/01", OtherRecipients = "Nơi nhận khác",
            Others = "Ghi chú tiếng Việt", Document = document
        };
        document.KindDetails = entity;
        fixture.Db.Add(entity);
        await fixture.Db.SaveChangesAsync();
        var scope = new V2ReadAuthority(V2EditingTests.Actor(), new HashSet<Guid> { document.Id });
        var response = await new V2DocumentQueries(fixture.Db).DetailAsync(document.Id, scope, default);

        Assert.NotNull(response.Details);
        Assert.IsNotType<DocumentKindDetails>((object)response.Details);
        var expected = JsonSerializer.Serialize(entity, WebJson);
        var actual = JsonSerializer.Serialize(response.Details, WebJson);
        Assert.Equal(expected, actual);
        using var json = JsonDocument.Parse(actual);
        Assert.Equal(14, json.RootElement.EnumerateObject().Count());
        Assert.False(json.RootElement.TryGetProperty("document", out _));
        Assert.False(json.RootElement.TryGetProperty("recipientPartnerIds", out _));
        Assert.False(json.RootElement.TryGetProperty("distributionTargetIds", out _));

        entity.Others = "Thay đổi entity sau khi đọc";
        Assert.Equal(actual, JsonSerializer.Serialize(response.Details, WebJson));
    }

    [Fact]
    public async Task Null_details_remain_null_in_the_HTTP_response()
    {
        await using var fixture = await V2EditingTests.Fixture.Create();
        var document = await fixture.Register("INTERNAL");
        var scope = new V2ReadAuthority(V2EditingTests.Actor(), new HashSet<Guid> { document.Id });
        var response = await new V2DocumentQueries(fixture.Db).DetailAsync(document.Id, scope, default);
        Assert.Null(response.Details);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(response, WebJson));
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("details").ValueKind);
    }

    [Fact]
    public async Task Nullable_fields_preserve_null_values_instead_of_fabricated_defaults()
    {
        await using var fixture = await V2EditingTests.Fixture.Create();
        var document = await fixture.Register("INTERNAL");
        var entity = new DocumentKindDetails { DocumentId = document.Id };
        fixture.Db.Add(entity);
        await fixture.Db.SaveChangesAsync();
        var scope = new V2ReadAuthority(V2EditingTests.Actor(), new HashSet<Guid> { document.Id });
        var response = await new V2DocumentQueries(fixture.Db).DetailAsync(document.Id, scope, default);
        Assert.NotNull(response.Details);
        Assert.Equal(JsonSerializer.Serialize(entity, WebJson), JsonSerializer.Serialize(response.Details, WebJson));
    }
}
