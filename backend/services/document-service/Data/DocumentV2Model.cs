using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace DocumentService;

internal static class DocumentV2Model
{
    public static void Configure(ModelBuilder model, bool sqlServer)
    {
        model.Entity<DocumentCurrentPdf>(e => {
            e.ToTable("DocumentCurrentPdfs", t => {
                t.HasCheckConstraint("CK_CurrentPdf_State", "[State] IN ('Pending','Ready','Missing')");
                t.HasCheckConstraint("CK_CurrentPdf_Size", "[SizeBytes] BETWEEN 1 AND 26214400");
                t.HasCheckConstraint("CK_CurrentPdf_Version", "[Version] >= 1");
            });
            e.HasKey(x => x.DocumentId); e.HasIndex(x => x.FileId).IsUnique(); e.HasIndex(x => x.OperationId).IsUnique();
            e.Property(x => x.State).HasMaxLength(16); e.Property(x => x.Sha256).HasMaxLength(64); e.Property(x => x.OriginalName).HasMaxLength(200);
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasOne<DocumentRegistration>().WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<PdfReplacement>(e => {
            e.ToTable("PdfReplacements", t => {
                t.HasCheckConstraint("CK_PdfReplacement_State", "[State] IN ('Preparing','Committed','Aborted')");
                t.HasCheckConstraint("CK_PdfReplacement_Version", "[ExpectedVersion] >= 1 AND ([CommittedVersion] IS NULL OR [CommittedVersion] > [ExpectedVersion])");
            });
            e.HasKey(x => x.OperationId); e.HasIndex(x => x.FileId).IsUnique();
            e.Property(x => x.State).HasMaxLength(16); e.Property(x => x.State).IsConcurrencyToken(); e.HasIndex(x => x.State);
            e.HasOne<DocumentRegistration>().WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<DocumentCancellation>(e => {
            e.ToTable("DocumentCancellations", t => {
                t.HasCheckConstraint("CK_DocumentCancellation_PreviousStatus", "[PreviousStatus] IN ('InProgress', 'Distributed')");
                t.HasCheckConstraint("CK_DocumentCancellation_Reason", sqlServer ? "LEN(TRIM([Reason])) > 0" : "LENGTH(TRIM([Reason])) > 0");
                t.HasCheckConstraint("CK_DocumentCancellation_Restore", "([RestoredAt] IS NULL AND [RestoredByUserId] IS NULL) OR ([RestoredAt] IS NOT NULL AND [RestoredByUserId] IS NOT NULL)");
            });
            e.HasKey(x => x.DocumentId); e.Property(x => x.PreviousStatus).HasMaxLength(16); e.Property(x => x.Reason).HasMaxLength(4000);
            e.HasOne<DocumentRegistration>().WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<DocumentRelation>(e =>
        {
            e.ToTable("DocumentRelations", t => t.HasCheckConstraint("CK_DocumentRelation_DifferentEnds", "[IncomingDocumentId] <> [OutgoingDocumentId]"));
            e.HasKey(x => new { x.IncomingDocumentId, x.OutgoingDocumentId });
            e.HasOne<DocumentRegistration>().WithMany().HasForeignKey(x => x.IncomingDocumentId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<DocumentRegistration>().WithMany().HasForeignKey(x => x.OutgoingDocumentId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.OutgoingDocumentId);
        });
        model.Entity<DocumentKindDetails>(e =>
        {
            e.ToTable("DocumentKindDetails");
            e.HasKey(x => x.DocumentId);
            e.HasOne(x => x.Document).WithOne(x => x.KindDetails).HasForeignKey<DocumentKindDetails>(x => x.DocumentId).OnDelete(DeleteBehavior.Cascade);
            e.Property(x => x.SenderNameSnapshot).HasMaxLength(200);
            e.Property(x => x.ReferenceNumber).HasMaxLength(200);
            e.Property(x => x.ContractNumber).HasMaxLength(200);
            e.Property(x => x.OtherRecipients).HasMaxLength(4000);
            e.Property(x => x.Others).HasMaxLength(4000);
            foreach (var name in new[] { nameof(DocumentKindDetails.MethodCode), nameof(DocumentKindDetails.DocumentTypeCode), nameof(DocumentKindDetails.CategoryCode) })
                e.Property(name).HasMaxLength(64);
            foreach (var name in new[] { nameof(DocumentKindDetails.MethodNameSnapshot), nameof(DocumentKindDetails.DocumentTypeNameSnapshot), nameof(DocumentKindDetails.CategoryNameSnapshot) })
                e.Property(name).HasMaxLength(200);
            e.HasIndex(x => x.SenderPartnerId);
        });
        model.Entity<DocumentRecipient>(e =>
        {
            e.ToTable("DocumentRecipients", t => t.HasCheckConstraint("CK_DocumentRecipient_Type", "[ReferenceType] IN ('ExternalEntity', 'DistributionTarget')"));
            e.HasKey(x => new { x.DocumentId, x.ReferenceType, x.ReferenceId });
            e.Property(x => x.ReferenceType).HasMaxLength(32);
            e.Property(x => x.NameSnapshot).HasMaxLength(200);
            e.HasOne(x => x.Document).WithMany(x => x.Recipients).HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.ReferenceType, x.ReferenceId });
        });
        model.Entity<DocumentRegistration>(e =>
        {
            e.ToTable("DocumentRegistrations", t =>
            {
                t.HasCheckConstraint("CK_Registration_Sequence", "[SequenceNumber] BETWEEN 1 AND 99999");
                t.HasCheckConstraint("CK_Registration_Year", sqlServer
                    ? "[RegistrationYear] = YEAR([RegistrationDate])"
                    : "[RegistrationYear] = CAST(strftime('%Y', [RegistrationDate]) AS INTEGER)");
                t.HasCheckConstraint("CK_Registration_Version", "[Version] >= 1");
                t.HasCheckConstraint("CK_Registration_Kind", "[Kind] IN ('INCOMING', 'OUTGOING', 'INTERNAL')");
                t.HasCheckConstraint("CK_Registration_Sensitivity", "[Sensitivity] IN ('Normal', 'Confidential')");
                t.HasCheckConstraint("CK_Registration_Company", "[CompanyCode] IN ('HL', 'HV', 'HLHV')");
            });
            e.HasKey(x => x.DocumentId);
            e.HasOne(x => x.Document).WithOne(x => x.Registration).HasForeignKey<DocumentRegistration>(x => x.DocumentId).OnDelete(DeleteBehavior.Cascade);
            e.Property(x => x.Kind).HasMaxLength(16);
            e.Property(x => x.CompanyCode).HasMaxLength(8);
            e.Property(x => x.CompanyNameSnapshot).HasMaxLength(200);
            e.Property(x => x.OwnerDepartmentCodeSnapshot).HasMaxLength(32);
            e.Property(x => x.OwnerDepartmentNameSnapshot).HasMaxLength(200);
            e.Property(x => x.Sensitivity).HasMaxLength(16);
            e.Property(x => x.Remark).HasMaxLength(4000);
            e.Property(x => x.Version).IsConcurrencyToken();
            // Generated identity/sequence fields remain immutable. Company/owner snapshots
            // change only through authorized edits; directory sync must not rewrite them.
            foreach (var property in new[] { nameof(DocumentRegistration.Kind), nameof(DocumentRegistration.RegistrationDate),
                nameof(DocumentRegistration.RegistrationYear), nameof(DocumentRegistration.SequenceNumber), nameof(DocumentRegistration.RegisteredAt),
                nameof(DocumentRegistration.InputterUserId) })
                e.Property(property).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
            e.HasIndex(x => new { x.Kind, x.RegistrationYear, x.SequenceNumber }).IsUnique();
            e.HasIndex(x => new { x.OwnerDepartmentId, x.RegistrationDate });
            e.HasIndex(x => x.InputterUserId);
            e.HasIndex(x => x.OriginatorUserId);
        });
        model.Entity<RegistrationRequest>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Kind).HasMaxLength(16);
            e.Property(x => x.KeyHash).HasMaxLength(64);
            e.Property(x => x.BodyHash).HasMaxLength(64);
            e.HasIndex(x => new { x.ActorUserId, x.Kind, x.KeyHash }).IsUnique();
            e.HasOne(x => x.Document).WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<DocumentEditAudit>(e =>
        {
            e.ToTable("DocumentEditAudits", t => t.HasCheckConstraint("CK_DocumentEditAudit_Version", "[Version] >= 2"));
            e.HasKey(x => x.Id);
            e.Property(x => x.ChangesJson).HasMaxLength(32000);
            e.HasIndex(x => new { x.DocumentId, x.Version }).IsUnique();
            e.HasOne<Document>().WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<DocumentOutboxEvent>(e =>
        {
            e.ToTable("DocumentOutboxEvents",t=>t.HasCheckConstraint("CK_DocumentOutbox_Attempts", "[Attempts] >= 0"));
            e.HasKey(x => x.Id);
            e.Property(x => x.Type).HasMaxLength(64);
            e.Property(x => x.State).HasMaxLength(16);
            e.Property(x => x.PayloadJson).HasMaxLength(1000);
            e.HasIndex(x => new { x.DocumentId, x.Type, x.AggregateVersion }).IsUnique();
            e.HasIndex(x => x.State);
            e.HasOne(x => x.Document).WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
