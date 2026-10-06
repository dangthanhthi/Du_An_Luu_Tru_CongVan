namespace DocumentService;

public static class ReminderEligibility
{
    public static bool IsEligible(string kind, string status, DateOnly registrationDate,
        bool hasAvailableCurrentPdf, DateOnly? issuedDate, DateTimeOffset now)
    {
        DocumentNumberFormatter.ValidateKind(kind);
        var completion = DocumentCompletionEvaluator.Evaluate(status, hasAvailableCurrentPdf, issuedDate);
        if (kind == "INCOMING" || status == "Cancelled" || completion.IsComplete) return false;
        var today = DocumentNumberFormatter.RegistrationDate(now);
        // Latest user confirmation: fourteen calendar days, with the Q31 kind/completeness rules.
        return today.DayNumber - registrationDate.DayNumber > 14;
    }
}
