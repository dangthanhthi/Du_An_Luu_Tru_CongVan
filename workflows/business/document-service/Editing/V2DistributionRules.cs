namespace DocumentService;

internal static class V2DistributionRules
{
    public static void Validate(string kind, DocumentKindDetails? details, IReadOnlyCollection<DocumentRecipient> recipients)
    {
        var requiredType = kind switch { "INCOMING" => "DistributionTarget", "OUTGOING" => "ExternalEntity", "INTERNAL" => null,
            _ => throw new DocumentRegistrationRuleException(409, "REGISTRATION_INCONSISTENT", "The document kind requires reconciliation.") };
        if (requiredType is null) {
            if (recipients.Count != 0) throw Invalid();
            return;
        }
        if (recipients.Count == 0 || recipients.Any(x => x.ReferenceType != requiredType || x.ReferenceId == Guid.Empty || string.IsNullOrWhiteSpace(x.NameSnapshot)))
            throw Invalid();
        if (kind == "INCOMING" && (details?.ReceivingDate is null || details.SenderPartnerId is null || details.SenderPartnerId == Guid.Empty ||
            string.IsNullOrWhiteSpace(details.SenderNameSnapshot) || string.IsNullOrWhiteSpace(details.MethodCode)))
            throw Invalid();
        // Existing snapshots are valid after catalog/directory deactivation. PDF/IssuedDate gate completion separately.
    }
    private static DocumentRegistrationRuleException Invalid() => new(400, "DISTRIBUTION_REQUIREMENTS", "Required distribution metadata and typed recipients must be present.");
}
