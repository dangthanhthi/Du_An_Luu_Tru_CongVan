using System.Text.Json;
using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Xunit;

namespace DocumentService.Tests;

public sealed class V2KindDetailsTests
{
    internal static readonly Guid PartnerA = Guid.Parse("aaaaaaaa-0000-4000-8000-000000000001");
    internal static readonly Guid PartnerB = Guid.Parse("bbbbbbbb-0000-4000-8000-000000000002");
    internal static V2ReferenceSet References(bool active = true) => new(new Dictionary<Guid, ExternalEntityReference> {
        [PartnerA] = new(PartnerA, "First partner", active), [PartnerB] = new(PartnerB, "Second partner", active) });
    internal static V2KindDetailsDraft Outgoing() => new(MethodCode: "EMAIL", DocumentTypeCode: "LETTER",
        ContractNumber: "Contract 1", OtherRecipients: "Other declared names", RecipientPartnerIds: [PartnerB, PartnerA]);
    internal static V2EditDraft Header(Document doc, V2KindDetailsDraft? details) => new(doc.Registration!.Version,
        doc.Registration.CompanyCode, doc.Title, doc.Registration.OriginatorUserId, doc.Registration.OwnerDepartmentId,
        doc.Registration.Sensitivity, doc.Registration.IssuedDate, doc.Registration.Remark, details);
    private static Task<Document> Register(DocumentDbContext db, string kind, V2KindDetailsDraft details, string key = "kind-details", V2ReferenceSet? refs = null) =>
        new V2RegistrationService(db, new V2PersistenceTests.Clock()).RegisterAsync(
            V2PersistenceTests.Draft(kind) with { Details = details }, V2PersistenceTests.Identity, key, references: refs ?? References());

    [Fact]
    public async Task Pre_details_header_receipt_remains_replayable_after_contract_extension()
    {
        await using var f = await V2EditingTests.Fixture.Create(); var draft = V2PersistenceTests.Draft();
        var service = new V2RegistrationService(f.Db, new V2PersistenceTests.Clock());
        var first = await service.RegisterAsync(draft, V2PersistenceTests.Identity, "old-contract");
        // The previous contract had exactly these fields; adding an optional field must not invalidate its persisted hash.
        var previousJson = JsonSerializer.Serialize(new { draft.Kind, draft.CompanyCode, draft.Subject, draft.OriginatorUserId,
            draft.OwnerDepartmentId, draft.Sensitivity, draft.IssuedDate, draft.Remark });
        var receipt = await f.Db.RegistrationRequests.SingleAsync();
        receipt.BodyHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(previousJson)));
        await f.Db.SaveChangesAsync();
        var replay = await service.RegisterAsync(draft, V2PersistenceTests.Identity, "old-contract");
        Assert.Equal(first.Id, replay.Id); Assert.Equal(1, (await f.Db.DocumentNumberCounters.SingleAsync()).CurrentValue);
    }

    [Theory]
    [InlineData("others")][InlineData("otherRecipients")][InlineData("contract")][InlineData("recipientCount")][InlineData("partnerName")]
    public async Task Oversized_details_or_verified_names_are_rejected_without_a_graph(string field)
    {
        await using var f = await V2EditingTests.Fixture.Create(); var details = Outgoing(); var refs = References();
        details = field switch { "others" => details with { Others = new string('Đ', 4001) },
            "otherRecipients" => details with { OtherRecipients = new string('x', 4001) },
            "contract" => details with { ContractNumber = new string('x', 201) },
            "recipientCount" => details with { RecipientPartnerIds = Enumerable.Range(0, 201).Select(_ => Guid.NewGuid()).ToArray() }, _ => details };
        if (field == "partnerName") refs = new(new Dictionary<Guid, ExternalEntityReference> { [PartnerA] = new(PartnerA, new string('Đ', 201), true) });
        var error = await Assert.ThrowsAsync<DocumentRegistrationRuleException>(() => Register(f.Db, "OUTGOING", details, refs: refs));
        Assert.Equal(400, error.Status); Assert.Empty(await f.Db.Documents.ToListAsync()); Assert.Empty(await f.Db.DocumentNumberCounters.ToListAsync());
    }

    [Fact]
    public async Task Distributed_incoming_can_edit_reference_and_targets_while_retaining_inactive_sender_and_target_names()
    {
        await using var f = await V2EditingTests.Fixture.Create(); var targets = BusinessCatalogSeed.Targets().Take(2).ToArray();
        var details = new V2KindDetailsDraft(new(2026, 12, 30), PartnerA, "Original ref", "EMAIL", DistributionTargetIds: targets.Select(x => x.Id).ToArray());
        var doc = await Register(f.Db, "INCOMING", details); doc.Status = "Distributed"; await f.Db.SaveChangesAsync();
        var target = await f.Db.DistributionTargets.SingleAsync(x => x.Id == targets[0].Id); target.Name = "Renamed"; target.IsActive = false; await f.Db.SaveChangesAsync();
        var result = await new V2DocumentEditor(f.Db, V2EditingTests.Clock).UpdateAsync(doc.Id,
            Header(doc, details with { ReferenceNumber = "Updated ref", DistributionTargetIds = [targets[0].Id] }), V2EditingTests.Actor(), references: References(false));
        Assert.Equal("Distributed", result.Status); Assert.Equal("Updated ref", result.KindDetails!.ReferenceNumber);
        Assert.Equal("First partner", result.KindDetails.SenderNameSnapshot); Assert.Equal(targets[0].Name, Assert.Single(result.Recipients).NameSnapshot);
        Assert.Equal(2, result.Registration!.Version); Assert.Equal(1, (await f.Db.DocumentNumberCounters.SingleAsync()).CurrentValue);
    }

    [Fact]
    public async Task Outgoing_multiple_recipients_and_catalog_snapshots_survive_a_fresh_context()
    {
        await using var f = await V2EditingTests.Fixture.Create();
        var doc = await Register(f.Db, "OUTGOING", Outgoing());
        await using var fresh = new DocumentDbContext(f.Options);
        var saved = await fresh.Set<DocumentKindDetails>().SingleAsync();
        Assert.Equal(doc.Id, saved.DocumentId); Assert.Equal("EMAIL", saved.MethodCode); Assert.Equal("eMail", saved.MethodNameSnapshot);
        Assert.Equal("LETTER", saved.DocumentTypeCode); Assert.Equal("Letter", saved.DocumentTypeNameSnapshot);
        Assert.Equal("Contract 1", saved.ContractNumber); Assert.Null(saved.SenderPartnerId);
        var recipients = await fresh.Set<DocumentRecipient>().OrderBy(x => x.NameSnapshot).ToListAsync();
        Assert.Equal(2, recipients.Count); Assert.Equal(PartnerA, recipients[0].ReferenceId); Assert.Equal("First partner", recipients[0].NameSnapshot);
        Assert.All(recipients, x => Assert.Equal("ExternalEntity", x.ReferenceType));
        Assert.Empty(await fresh.DocumentDepartmentAccess.ToListAsync()); // Metadata selection is not an access grant.
        Assert.Equal(1, (await fresh.DocumentNumberCounters.SingleAsync()).CurrentValue);
    }

    [Fact]
    public async Task Incoming_keeps_sender_receiving_date_and_multiple_declared_targets_without_granting_access()
    {
        await using var f = await V2EditingTests.Fixture.Create();
        var targets = BusinessCatalogSeed.Targets().Take(2).Select(x => x.Id).ToArray();
        var doc = await Register(f.Db, "INCOMING", new(new(2026, 12, 30), PartnerA, "External reference", "FAX", "LETTER", DistributionTargetIds: targets));
        f.Db.ChangeTracker.Clear(); var details = await f.Db.Set<DocumentKindDetails>().SingleAsync();
        Assert.Equal(new DateOnly(2026, 12, 30), details.ReceivingDate); Assert.Equal(PartnerA, details.SenderPartnerId);
        Assert.Equal("First partner", details.SenderNameSnapshot); Assert.Equal("External reference", details.ReferenceNumber);
        Assert.Equal(new DateOnly(2027, 1, 1), (await f.Db.DocumentRegistrations.SingleAsync()).RegistrationDate);
        Assert.Equal(2, await f.Db.Set<DocumentRecipient>().CountAsync()); Assert.Empty(await f.Db.DocumentDepartmentAccess.ToListAsync());
        Assert.Equal("27-01-0001/HL", doc.DocumentNumber);
    }

    [Fact]
    public async Task Internal_uses_its_separate_type_catalog_and_has_no_recipients()
    {
        await using var f = await V2EditingTests.Fixture.Create(); await Register(f.Db, "INTERNAL", new(DocumentTypeCode: "MEMO", Others: "Internal note"));
        var details = await f.Db.Set<DocumentKindDetails>().SingleAsync(); Assert.Equal("MEMO", details.DocumentTypeCode);
        Assert.Equal("Inter-Office Memo", details.DocumentTypeNameSnapshot); Assert.Equal("Internal note", details.Others);
        Assert.Empty(await f.Db.Set<DocumentRecipient>().ToListAsync());
    }

    [Theory]
    [InlineData("internalExternal")][InlineData("internalTarget")][InlineData("internalMethod")][InlineData("outgoingSender")]
    [InlineData("outgoingTarget")][InlineData("incomingExternal")][InlineData("incomingMissingSender")][InlineData("incomingMissingDate")]
    [InlineData("incomingMissingMethod")][InlineData("duplicate")][InlineData("emptyId")][InlineData("longText")][InlineData("wrongType")]
    public async Task Invalid_kind_shapes_are_rejected_before_allocating_a_number(string invalid)
    {
        await using var f = await V2EditingTests.Fixture.Create(); var kind = "OUTGOING"; var details = Outgoing();
        switch (invalid)
        {
            case "internalExternal": kind = "INTERNAL"; details = new(RecipientPartnerIds: [PartnerA]); break;
            case "internalTarget": kind = "INTERNAL"; details = new(DistributionTargetIds: [Guid.NewGuid()]); break;
            case "internalMethod": kind = "INTERNAL"; details = new(MethodCode: "EMAIL"); break;
            case "outgoingSender": details = details with { SenderPartnerId = PartnerA }; break;
            case "outgoingTarget": details = details with { DistributionTargetIds = [Guid.NewGuid()] }; break;
            case "incomingExternal": kind = "INCOMING"; details = new(new(2027, 1, 1), PartnerA, MethodCode: "EMAIL", RecipientPartnerIds: [PartnerA]); break;
            case "incomingMissingSender": kind = "INCOMING"; details = new(new(2027, 1, 1), MethodCode: "EMAIL"); break;
            case "incomingMissingDate": kind = "INCOMING"; details = new(SenderPartnerId: PartnerA, MethodCode: "EMAIL"); break;
            case "incomingMissingMethod": kind = "INCOMING"; details = new(new(2027, 1, 1), PartnerA); break;
            case "duplicate": details = details with { RecipientPartnerIds = [PartnerA, PartnerA] }; break;
            case "emptyId": details = details with { RecipientPartnerIds = [Guid.Empty] }; break;
            case "longText": details = details with { ReferenceNumber = new string('x', 201) }; break;
            case "wrongType": kind = "INTERNAL"; details = new(DocumentTypeCode: "LETTER"); break;
        }
        var error = await Assert.ThrowsAsync<DocumentRegistrationRuleException>(() => Register(f.Db, kind, details));
        Assert.Equal(400, error.Status); Assert.Empty(await f.Db.Documents.ToListAsync()); Assert.Empty(await f.Db.DocumentNumberCounters.ToListAsync());
        Assert.Empty(await f.Db.RegistrationRequests.ToListAsync()); Assert.Empty(await f.Db.DocumentOutboxEvents.ToListAsync());
    }

    [Theory]
    [InlineData("inactivePartner")][InlineData("missingPartner")][InlineData("mismatchedPartner")][InlineData("inactiveMethod")][InlineData("missingCategory")][InlineData("inactiveTarget")]
    public async Task New_selections_require_active_verified_references(string invalid)
    {
        await using var f = await V2EditingTests.Fixture.Create(); var kind = "OUTGOING"; var details = Outgoing(); var refs = References();
        if (invalid == "inactivePartner") refs = References(false);
        if (invalid == "missingPartner") refs = new(new Dictionary<Guid, ExternalEntityReference>());
        if (invalid == "mismatchedPartner") refs = new(new Dictionary<Guid, ExternalEntityReference> { [PartnerA] = new(PartnerB, "Wrong", true) });
        if (invalid == "inactiveMethod") { (await f.Db.BusinessCatalogEntries.SingleAsync(x => x.Group == "methods" && x.Code == "EMAIL")).IsActive = false; await f.Db.SaveChangesAsync(); }
        if (invalid == "missingCategory") details = details with { CategoryCode = "MISSING" };
        if (invalid == "inactiveTarget")
        {
            var t = await f.Db.DistributionTargets.FirstAsync(); t.IsActive = false; await f.Db.SaveChangesAsync();
            kind = "INCOMING"; details = new(new(2027, 1, 1), PartnerA, MethodCode: "EMAIL", DistributionTargetIds: [t.Id]);
        }
        var error = await Assert.ThrowsAsync<DocumentRegistrationRuleException>(() => Register(f.Db, kind, details, refs: refs));
        Assert.Equal(400, error.Status); Assert.Empty(await f.Db.Documents.ToListAsync()); Assert.Empty(await f.Db.DocumentNumberCounters.ToListAsync());
    }

    [Fact]
    public async Task Replay_normalizes_recipient_order_and_text_and_preserves_original_snapshots()
    {
        await using var f = await V2EditingTests.Fixture.Create(); var details = Outgoing(); var first = await Register(f.Db, "OUTGOING", details);
        (await f.Db.BusinessCatalogEntries.SingleAsync(x => x.Group == "methods" && x.Code == "EMAIL")).IsActive = false; await f.Db.SaveChangesAsync();
        var replay = await Register(f.Db, "OUTGOING", details with { MethodCode = " email ", ContractNumber = " Contract 1 ", RecipientPartnerIds = [PartnerA, PartnerB] }, refs: References(false));
        Assert.Equal(first.Id, replay.Id); Assert.NotNull(replay.KindDetails); Assert.Equal("eMail", replay.KindDetails!.MethodNameSnapshot);
        Assert.Equal(2, replay.Recipients.Count); Assert.Equal(1, (await f.Db.DocumentNumberCounters.SingleAsync()).CurrentValue);
    }

    [Fact]
    public async Task Different_recipient_intent_with_the_same_key_conflicts_without_a_second_graph()
    {
        await using var f = await V2EditingTests.Fixture.Create(); await Register(f.Db, "OUTGOING", Outgoing());
        var error = await Assert.ThrowsAsync<DocumentRegistrationRuleException>(() => Register(f.Db, "OUTGOING", Outgoing() with { RecipientPartnerIds = [PartnerA] }));
        Assert.Equal(409, error.Status); Assert.Equal(2, await f.Db.Set<DocumentRecipient>().CountAsync()); Assert.Single(await f.Db.Documents.ToListAsync());
    }

    [Fact]
    public async Task Details_and_header_edit_share_one_version_audit_event_and_counter()
    {
        await using var f = await V2EditingTests.Fixture.Create(); var doc = await Register(f.Db, "OUTGOING", Outgoing());
        var details = Outgoing() with { RecipientPartnerIds = [PartnerB], MethodCode = "FAX", ContractNumber = "Changed contract" };
        var draft = Header(doc, details) with { Subject = "Changed subject", CompanyCode = "HV" };
        var edited = await new V2DocumentEditor(f.Db, V2EditingTests.Clock).UpdateAsync(doc.Id, draft, V2EditingTests.Actor(), references: References());
        Assert.Equal(2, edited.Registration!.Version); Assert.Equal("27-01-0001/HV/ADM", edited.DocumentNumber);
        f.Db.ChangeTracker.Clear(); Assert.Equal("Changed contract", (await f.Db.Set<DocumentKindDetails>().SingleAsync()).ContractNumber);
        Assert.Equal(PartnerB, (await f.Db.Set<DocumentRecipient>().SingleAsync()).ReferenceId);
        var audit = await f.Db.Set<DocumentEditAudit>().SingleAsync(); using var changes = JsonDocument.Parse(audit.ChangesJson);
        var detailChange = changes.RootElement.GetProperty("KindDetails");
        Assert.Equal(2, detailChange.GetProperty("before").GetProperty("Recipients").GetArrayLength());
        Assert.Equal(1, detailChange.GetProperty("after").GetProperty("Recipients").GetArrayLength());
        Assert.Equal("FAX", detailChange.GetProperty("after").GetProperty("MethodCode").GetString());
        Assert.Equal(2, await f.Db.DocumentOutboxEvents.CountAsync()); Assert.Equal(1, (await f.Db.DocumentNumberCounters.SingleAsync()).CurrentValue);
        Assert.Empty(await f.Db.DocumentDepartmentAccess.ToListAsync());
    }

    [Fact]
    public async Task Unchanged_inactive_selections_keep_historical_names_and_a_normalized_noop_keeps_version()
    {
        await using var f = await V2EditingTests.Fixture.Create(); var doc = await Register(f.Db, "OUTGOING", Outgoing());
        var method = await f.Db.BusinessCatalogEntries.SingleAsync(x => x.Group == "methods" && x.Code == "EMAIL"); method.IsActive = false; method.Name = "Renamed"; await f.Db.SaveChangesAsync();
        var result = await new V2DocumentEditor(f.Db, V2EditingTests.Clock).UpdateAsync(doc.Id, Header(doc, Outgoing() with { RecipientPartnerIds = [PartnerA, PartnerB] }), V2EditingTests.Actor(), references: References(false));
        Assert.Equal(1, result.Registration!.Version); Assert.Equal("eMail", result.KindDetails!.MethodNameSnapshot);
        Assert.Equal("First partner", result.Recipients.Single(x => x.ReferenceId == PartnerA).NameSnapshot); Assert.Empty(await f.Db.Set<DocumentEditAudit>().ToListAsync());
    }

    [Fact]
    public async Task Removed_inactive_recipient_cannot_be_added_again_as_a_new_selection()
    {
        await using var f = await V2EditingTests.Fixture.Create(); var doc = await Register(f.Db, "OUTGOING", Outgoing());
        var editor = new V2DocumentEditor(f.Db, V2EditingTests.Clock);
        var updated = await editor.UpdateAsync(doc.Id, Header(doc, Outgoing() with { RecipientPartnerIds = [PartnerB] }), V2EditingTests.Actor(), references: References(false));
        var error = await Assert.ThrowsAsync<DocumentRegistrationRuleException>(() => editor.UpdateAsync(doc.Id, Header(updated, Outgoing()), V2EditingTests.Actor(), references: References(false)));
        Assert.Equal(400, error.Status); Assert.Equal(PartnerB, (await f.Db.Set<DocumentRecipient>().SingleAsync()).ReferenceId); Assert.Equal(2, (await f.Db.DocumentRegistrations.SingleAsync()).Version);
    }

    [Fact]
    public async Task Header_only_edit_preserves_details_and_recipient_snapshots()
    {
        await using var f = await V2EditingTests.Fixture.Create(); var doc = await Register(f.Db, "OUTGOING", Outgoing());
        await new V2DocumentEditor(f.Db, V2EditingTests.Clock).UpdateAsync(doc.Id, Header(doc, null) with { Subject = "Header only" }, V2EditingTests.Actor());
        Assert.Equal(2, await f.Db.Set<DocumentRecipient>().CountAsync()); Assert.Equal("Contract 1", (await f.Db.Set<DocumentKindDetails>().SingleAsync()).ContractNumber);
    }

    [Theory]
    [InlineData("stale")][InlineData("forbidden")][InlineData("badReference")]
    public async Task Rejected_edits_leave_header_details_and_recipients_intact(string invalid)
    {
        await using var f = await V2EditingTests.Fixture.Create(); var doc = await Register(f.Db, "OUTGOING", Outgoing());
        var draft = Header(doc, Outgoing() with { RecipientPartnerIds = [] }) with { Subject = "Uncommitted" }; var actor = V2EditingTests.Actor();
        if (invalid == "stale") draft = draft with { ExpectedVersion = 7 };
        if (invalid == "forbidden") actor = V2EditingTests.Actor(Guid.NewGuid());
        if (invalid == "badReference") draft = draft with { Details = Outgoing() with { MethodCode = "INVALID" } };
        await Assert.ThrowsAsync<DocumentRegistrationRuleException>(() => new V2DocumentEditor(f.Db, V2EditingTests.Clock).UpdateAsync(doc.Id, draft, actor));
        Assert.Equal(2, await f.Db.Set<DocumentRecipient>().CountAsync()); Assert.Equal("Registered subject", (await f.Db.Documents.SingleAsync()).Title);
        Assert.Equal(1, (await f.Db.DocumentRegistrations.SingleAsync()).Version); Assert.Empty(await f.Db.Set<DocumentEditAudit>().ToListAsync());
    }

    [Fact]
    public async Task Audit_save_failure_rolls_back_recipient_removal_details_and_header_then_allows_retry()
    {
        var fault = new V2EditingTests.AuditFailure(); await using var f = await V2EditingTests.Fixture.Create(fault);
        var doc = await Register(f.Db, "OUTGOING", Outgoing()); var editor = new V2DocumentEditor(f.Db, V2EditingTests.Clock);
        var draft = Header(doc, Outgoing() with { RecipientPartnerIds = [], ContractNumber = "Uncommitted" }) with { Subject = "Changed" };
        await Assert.ThrowsAsync<IOException>(() => editor.UpdateAsync(doc.Id, draft, V2EditingTests.Actor()));
        Assert.False(f.Db.ChangeTracker.HasChanges()); Assert.Equal(2, await f.Db.Set<DocumentRecipient>().CountAsync());
        Assert.Equal("Contract 1", (await f.Db.Set<DocumentKindDetails>().SingleAsync()).ContractNumber); Assert.Equal(1, await f.Db.DocumentOutboxEvents.CountAsync());
        fault.Enabled = false; await editor.UpdateAsync(doc.Id, draft, V2EditingTests.Actor()); Assert.Empty(await f.Db.Set<DocumentRecipient>().ToListAsync());
    }

    [Fact]
    public async Task Failure_after_registration_SaveChanges_rolls_back_the_entire_persisted_graph_and_key()
    {
        var fault = new AfterSaveFailure(); await using var f = await V2EditingTests.Fixture.Create(fault);
        await Assert.ThrowsAsync<IOException>(() => Register(f.Db, "OUTGOING", Outgoing()));
        Assert.False(f.Db.ChangeTracker.HasChanges()); Assert.Empty(await f.Db.Documents.ToListAsync());
        Assert.Empty(await f.Db.Set<DocumentKindDetails>().ToListAsync()); Assert.Empty(await f.Db.Set<DocumentRecipient>().ToListAsync());
        Assert.Empty(await f.Db.DocumentRegistrations.ToListAsync()); Assert.Empty(await f.Db.RegistrationRequests.ToListAsync());
        Assert.Empty(await f.Db.DocumentOutboxEvents.ToListAsync()); Assert.Empty(await f.Db.DocumentNumberCounters.ToListAsync());
        fault.Enabled = false; var retry = await Register(f.Db, "OUTGOING", Outgoing());
        Assert.Equal("27-01-0001/HL/ADM", retry.DocumentNumber); Assert.Equal(2, await f.Db.Set<DocumentRecipient>().CountAsync());
    }

    [Fact]
    public async Task SQLite_additive_upgrade_preserves_prior_header_and_counter_and_supports_details_after_restart()
    {
        await using var f = await V2EditingTests.Fixture.Create(); var first = await f.Register();
        await f.Db.Database.ExecuteSqlRawAsync("DROP TABLE DocumentRecipients; DROP TABLE DocumentKindDetails;");
        await SqliteG1Upgrade.ApplyAsync(f.Db); await SqliteG1Upgrade.ApplyAsync(f.Db);
        await using var fresh = new DocumentDbContext(f.Options);
        var header = await fresh.DocumentRegistrations.SingleAsync(); Assert.Equal(1, header.Version);
        Assert.Equal(1, (await fresh.DocumentNumberCounters.SingleAsync()).CurrentValue); Assert.Empty(await fresh.Set<DocumentKindDetails>().ToListAsync());
        var updated = await new V2DocumentEditor(fresh, V2EditingTests.Clock).UpdateAsync(first.Id,
            Header(first, Outgoing()), V2EditingTests.Actor(), references: References());
        Assert.Equal(2, updated.Registration!.Version); Assert.Equal(2, await fresh.Set<DocumentRecipient>().CountAsync());
    }

    [Fact]
    public async Task Lost_ack_after_actual_details_edit_COMMIT_does_not_repeat_recipient_mutations()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        var fault = new LostCommit();
        var options = new DbContextOptionsBuilder<DocumentDbContext>().UseSqlite(connection, sql => sql.ExecutionStrategy(d => new RetryOnce(d))).AddInterceptors(fault).Options;
        await using var db = new DocumentDbContext(options); await db.Database.EnsureCreatedAsync();
        var doc = await Register(db, "OUTGOING", Outgoing()); fault.Enabled = true;
        var result = await new V2DocumentEditor(db, V2EditingTests.Clock).UpdateAsync(doc.Id,
            Header(doc, Outgoing() with { RecipientPartnerIds = [PartnerA], ContractNumber = "Committed contract" }), V2EditingTests.Actor());
        Assert.Equal(1, fault.Failures); Assert.Equal(2, result.Registration!.Version); Assert.Equal(PartnerA, Assert.Single(result.Recipients).ReferenceId);
        Assert.Equal("Committed contract", result.KindDetails!.ContractNumber); Assert.Single(await db.Set<DocumentEditAudit>().ToListAsync());
        Assert.Equal(2, await db.DocumentOutboxEvents.CountAsync()); Assert.Equal(1, (await db.DocumentNumberCounters.SingleAsync()).CurrentValue);
    }

    private sealed class AfterSaveFailure : SaveChangesInterceptor
    {
        public bool Enabled = true;
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData data, int result, CancellationToken ct = default)
        { if (Enabled) throw new IOException("Injected failure after rows were saved, before COMMIT"); return ValueTask.FromResult(result); }
    }
    private sealed class RetryOnce(ExecutionStrategyDependencies dependencies) : ExecutionStrategy(dependencies, 1, TimeSpan.Zero)
    { protected override bool ShouldRetryOn(Exception error) => error is TimeoutException; }
    private sealed class LostCommit : DbTransactionInterceptor
    {
        public bool Enabled; public int Failures;
        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData data, CancellationToken ct = default)
        { if (Enabled) { Enabled = false; Failures++; throw new TimeoutException("Injected acknowledgement loss after actual COMMIT"); } return Task.CompletedTask; }
    }
}
