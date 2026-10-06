using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;
namespace DocumentService.Tests;

public sealed class CatalogTests
{
    [Fact]
    public async Task Catalogs_preserve_source_labels_and_exact_five_methods_with_optional_empty_category()
    {
        await using var f = await Fixture.Create();
        await using var db = f.Db();
        var data = await new CatalogService(db, TimeProvider.System).GetAsync(null);
        Assert.Equal(3, data["companies"].Count);
        Assert.Equal(5, data["methods"].Count);
        Assert.Contains(data["methods"], x => x.Name == "Pick-up/Hand-Deliver");
        Assert.Equal(6, data["documentTypes"].Count);
        Assert.Equal(5, data["internalTypes"].Count);
        Assert.Equal(2, data["sensitivity"].Count);
        Assert.Empty(data["categories"]);
    }
    [Fact]
    public async Task Recipient_labels_have_no_implicit_department_mapping_and_keep_historical_codes()
    {
        await using var f = await Fixture.Create();
        await using var db = f.Db();
        var rows = await db.Set<DistributionTarget>().ToListAsync();
        Assert.Equal(14, rows.Count);
        Assert.All(rows, x => Assert.Equal("Pending", x.MappingState));
        Assert.Equal("MGM", rows.Single(x => x.LegacyId == 1).Initial);
        Assert.Equal("DRI", rows.Single(x => x.LegacyId == 9).Initial);
        Assert.Equal("HSE", rows.Single(x => x.LegacyId == 3).Initial);
    }
    [Fact]
    public async Task Create_normalizes_code_and_audits_without_modifying_seed_ids()
    {
        await using var f = await Fixture.Create();
        await using var db = f.Db();
        var actor = Guid.NewGuid();
        var saved = await new CatalogService(db, TimeProvider.System).CreateAsync("categories", "  legal ", "  Pháp lý ", actor);
        Assert.Equal("LEGAL", saved.Code);
        Assert.Equal("Pháp lý", saved.Name);
        Assert.True(saved.IsActive);
        var audit = Assert.Single(await db.Set<CatalogAuditEvent>().ToListAsync());
        Assert.Equal(actor, audit.ActorUserId);
        Assert.Equal(saved.Id, audit.EntryId);
        Assert.Equal("Created", audit.Action);
    }
    [Fact]
    public async Task Duplicate_code_is_409_but_same_code_in_different_catalog_is_valid()
    {
        await using var f = await Fixture.Create();
        await using var db = f.Db();
        var service = new CatalogService(db, TimeProvider.System);
        var actor = Guid.NewGuid();
        var duplicate = await Assert.ThrowsAsync<CatalogRuleException>(() => service.CreateAsync("methods", "email", "Copy", actor));
        Assert.Equal(409, duplicate.Status);
        var other = await service.CreateAsync("categories", "email", "Email", actor);
        Assert.Equal("categories", other.Group);
    }
    [Fact]
    public async Task Deactivation_preserves_row_and_audit_but_removes_new_selection()
    {
        await using var f = await Fixture.Create();
        await using var db = f.Db();
        var service = new CatalogService(db, TimeProvider.System);
        var row = (await service.GetAsync("methods"))["methods"][0];
        var changed = await service.UpdateAsync(row.Id, new(row.Name, row.SortOrder, false, row.Version), Guid.NewGuid());
        Assert.False(changed.IsActive);
        Assert.Equal(2, changed.Version);
        Assert.DoesNotContain((await service.GetAsync("methods"))["methods"], x => x.Id == row.Id);
        Assert.NotNull(await db.Set<BusinessCatalogEntry>().FindAsync(row.Id));
        Assert.Single(await db.Set<CatalogAuditEvent>().ToListAsync());
    }
    [Fact]
    public async Task Stale_version_is_409_and_cannot_change_metadata_or_add_audit()
    {
        await using var f = await Fixture.Create();
        await using var db = f.Db();
        var service = new CatalogService(db, TimeProvider.System);
        var row = (await service.GetAsync("methods"))["methods"][0];
        var actor = Guid.NewGuid();
        await service.UpdateAsync(row.Id, new("Updated", 1, true, 1), actor);
        var stale = await Assert.ThrowsAsync<CatalogRuleException>(() => service.UpdateAsync(row.Id, new("Overwrite", 1, true, 1), actor));
        Assert.Equal(409, stale.Status);
        Assert.Equal("Updated", (await service.GetAsync("methods"))["methods"].Single(x => x.Id == row.Id).Name);
        Assert.Single(await db.Set<CatalogAuditEvent>().ToListAsync());
    }
    [Theory]
    [InlineData("unknown", "X", "Name")]
    [InlineData("methods", "BAD/CODE", "Name")]
    [InlineData("methods", "X", " ")]
    [InlineData("companies", "NEW", "Unsupported legal entity")]
    [InlineData("sensitivity", "PUBLIC", "Custom sensitivity")]
    public async Task Invalid_or_fixed_master_changes_are_rejected(string group, string code, string name)
    {
        await using var f = await Fixture.Create();
        await using var db = f.Db();
        var error = await Assert.ThrowsAsync<CatalogRuleException>(() => new CatalogService(db, TimeProvider.System).CreateAsync(group, code, name, Guid.NewGuid()));
        Assert.Equal(400, error.Status);
        Assert.Empty(await db.Set<CatalogAuditEvent>().ToListAsync());
    }
    [Fact]
    public async Task Unrecognized_requested_group_cannot_silently_become_empty_success()
    {
        await using var f = await Fixture.Create();
        await using var db = f.Db();
        var error = await Assert.ThrowsAsync<CatalogRuleException>(() => new CatalogService(db, TimeProvider.System).GetAsync("methods,typo"));
        Assert.Equal(400, error.Status);
    }
    internal sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection = new("Data Source=:memory:");
        public DocumentDbContext Db() => new(new DbContextOptionsBuilder<DocumentDbContext>().UseSqlite(connection).Options);
        public static async Task<Fixture> Create()
        {
            var f = new Fixture(); await f.connection.OpenAsync(); await using var db = f.Db();
            await db.Database.EnsureCreatedAsync(); return f;
        }
        public async ValueTask DisposeAsync() => await connection.DisposeAsync();
    }
}

