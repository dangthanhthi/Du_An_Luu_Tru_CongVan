using EmailWorkerService.Services;
using Xunit;

[Trait("Suite", "LegacyBaseline")]
public sealed class EmailFieldParserBaselineTests
{
    [Fact]
    public void Empty_email_does_not_fabricate_a_reference_or_date()
    {
        var result = new EmailFieldParser().ParseEmailFields(null, null, null, null, null);
        Assert.Null(result.ReferenceNumber);
        Assert.Null(result.DocumentDate);
    }

    [Fact]
    public void Explicit_document_date_takes_precedence_over_email_sent_date()
    {
        var result = new EmailFieldParser().ParseEmailFields("Số: 2474/DAN-QLDN2",
            "Ngày ban hành: 14/11/2025", null, null, new DateTime(2026, 10, 4));
        Assert.Equal("2474/DAN-QLDN2", result.ReferenceNumber);
        Assert.Equal(new DateTime(2025, 11, 14), result.DocumentDate);
    }

    [Fact]
    public void Legacy_sent_date_fallback_has_low_confidence()
    {
        var result = new EmailFieldParser().ParseEmailFields("Công văn", null, null, null, new DateTime(2026, 10, 4));
        Assert.Equal(new DateTime(2026, 10, 4), result.DocumentDate);
        Assert.Equal(10, result.DateConfidence);
    }
}
