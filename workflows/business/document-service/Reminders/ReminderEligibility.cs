namespace DocumentService;

public static class ReminderEligibility
{
    public const int AgeThresholdDays = 7;
    public static bool IsEligible(string kind, string status, DateOnly registrationDate,
        bool hasAvailableCurrentPdf, DateOnly? issuedDate, DateTimeOffset now)
    {
        DocumentNumberFormatter.ValidateKind(kind);
        var completion = DocumentCompletionEvaluator.Evaluate(status, hasAvailableCurrentPdf, issuedDate);
        if (kind == "INCOMING" || status == "Cancelled" || completion.IsComplete) return false;
        var today = DocumentNumberFormatter.RegistrationDate(now);
        // Official mentor clarification: more than seven calendar days in Vietnam.
        return today.DayNumber - registrationDate.DayNumber > AgeThresholdDays;
    }
}
