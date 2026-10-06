namespace FilesService.Storage;

public sealed record PdfStorageOptions(string Root, long MaxBytes)
{
    public const long DefaultMaxBytes = 25L * 1024 * 1024;
    public static PdfStorageOptions Read(IConfiguration config)
    {
        var root = config["Storage:Path"];
        if (string.IsNullOrWhiteSpace(root) || !Path.IsPathFullyQualified(root)) throw Invalid();
        root = Path.GetFullPath(root);
        if (!Directory.Exists(root) || (File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0) throw Invalid();
        var rawMax = config["Storage:MaxBytes"];
        if (rawMax is not null && (!long.TryParse(rawMax, out var parsed) || parsed < 1 || parsed > DefaultMaxBytes)) throw Invalid();
        return new(root, rawMax is null ? DefaultMaxBytes : long.Parse(rawMax));
    }
    public string PathFor(Guid id, bool temporary = false)
    {
        if (!Directory.Exists(Root) || (File.GetAttributes(Root) & FileAttributes.ReparsePoint) != 0) throw Invalid();
        var path = Path.Combine(Root, id.ToString("N") + (temporary ? ".upload" : ".pdf"));
        if (File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw Invalid();
        return path;
    }
    private static InvalidOperationException Invalid() => new("Configure an existing absolute non-link Storage:Path and Storage:MaxBytes within 1..25 MiB; no storage fallback is supported.");
}
