namespace DocumentService;

// Read-only snapshot. Keep the existing details JSON fields without exposing EF navigation.
public sealed record V2KindDetailsView(Guid DocumentId, DateOnly? ReceivingDate, Guid? SenderPartnerId,
    string? SenderNameSnapshot, string? ReferenceNumber, string? MethodCode, string? MethodNameSnapshot,
    string? DocumentTypeCode, string? DocumentTypeNameSnapshot, string? CategoryCode,
    string? CategoryNameSnapshot, string? ContractNumber, string? OtherRecipients, string? Others);
