namespace DocumentService;

public sealed record CompletionAssessment(bool IsComplete, IReadOnlyList<string> Issues);

public static class DocumentCompletionEvaluator
{
    public static CompletionAssessment Evaluate(string status, bool hasAvailableCurrentPdf, DateOnly? issuedDate)
    {
        if (status is not ("InProgress" or "Distributed" or "Cancelled"))
            throw new ArgumentException("Unknown v2 document status.", nameof(status));
        var issues = new List<string>();
        if (status != "Distributed") issues.Add("NotDistributed");
        if (!hasAvailableCurrentPdf) issues.Add("MissingPdf");
        if (!issuedDate.HasValue) issues.Add("MissingIssuedDate");
        return new(issues.Count == 0, issues.AsReadOnly());
    }
}
