namespace FilesService.Services;

public sealed class FileRuleException(int status, string code, string message) : Exception(message)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
}
