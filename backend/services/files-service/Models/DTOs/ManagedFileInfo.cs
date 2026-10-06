namespace FilesService.Models.DTOs;
public sealed record ManagedFileInfo(Guid Id, string OriginalName, string ContentType, long SizeBytes,
    string Sha256, string State, bool CanDownload, bool CanAttach);
