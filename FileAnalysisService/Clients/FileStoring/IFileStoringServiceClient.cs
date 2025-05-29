namespace FileAnalysisService.Clients.FileStoring;

public class FileData
{
    public Stream Content { get; set; }
    public string ContentType { get; set; }
    public string OriginalFileName { get; set; }
}

public interface IFileStoringServiceClient
{
    Task<FileData?> GetFileDataAsync(Guid fileId);
}