using System.Globalization;
using System.Text.RegularExpressions;

namespace DocumentService;

public static partial class DocumentNumberFormatter
{
    private static readonly TimeZoneInfo BusinessZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");

    public static DateOnly RegistrationDate(DateTimeOffset registeredAt) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(registeredAt, BusinessZone).DateTime);

    public static string Format(string kind, DateOnly registrationDate, int sequence, string companyCode, string? departmentCode = null)
    {
        ValidateKind(kind);
        if (sequence < 1) throw new ArgumentOutOfRangeException(nameof(sequence));
        // D02 confirms five digits after 9999. Further expansion is still D02b.
        if (sequence > 99999) throw new InvalidOperationException("Numbering beyond five digits requires a confirmed policy.");
        companyCode = companyCode.Trim().ToUpperInvariant();
        if (companyCode is not ("HL" or "HV" or "HLHV")) throw new ArgumentException("Invalid company code.", nameof(companyCode));
        departmentCode = departmentCode?.Trim().ToUpperInvariant();
        if (kind != DocumentTypeConstants.INCOMING && (departmentCode is null || !DepartmentCodePattern().IsMatch(departmentCode)))
            throw new ArgumentException("A validated department code is required.", nameof(departmentCode));
        if (kind == DocumentTypeConstants.INCOMING && !string.IsNullOrEmpty(departmentCode))
            throw new ArgumentException("Incoming numbering does not contain a department code.", nameof(departmentCode));
        var date = registrationDate.ToString("yy-MM", CultureInfo.InvariantCulture);
        var value = sequence.ToString("D4", CultureInfo.InvariantCulture);
        return kind switch
        {
            DocumentTypeConstants.INCOMING => $"{date}-{value}/{companyCode}",
            DocumentTypeConstants.OUTGOING => $"{date}-{value}/{companyCode}/{departmentCode}",
            _ => $"{date}-{value}/INT/{companyCode}/{departmentCode}"
        };
    }

    // Explicit v1 format for existing routes; no historical numbers are rewritten.
    public static string FormatLegacy(string kind, DateOnly date, int sequence)
    {
        ValidateKind(kind);
        var prefix = kind switch { "INCOMING" => "CV-DEN", "OUTGOING" => "CV-DI", _ => "CV-NB" };
        return $"{prefix}-{date.Year}-{sequence.ToString("D4", CultureInfo.InvariantCulture)}";
    }

    public static void ValidateKind(string kind)
    {
        if (kind is not (DocumentTypeConstants.INCOMING or DocumentTypeConstants.OUTGOING or DocumentTypeConstants.INTERNAL))
            throw new ArgumentException("Unknown document kind.", nameof(kind));
    }

    [GeneratedRegex(@"^[A-Z][A-Z0-9&-]{0,31}$", RegexOptions.CultureInvariant)]
    private static partial Regex DepartmentCodePattern();
}
