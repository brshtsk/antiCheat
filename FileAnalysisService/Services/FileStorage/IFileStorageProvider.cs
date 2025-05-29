namespace FileAnalysisService.Services.FileStorage;

public enum ContentType
{
    Image,
    Text,
}

public interface IFileStorageProvider
{
    Task<string> SaveFileAsync(Stream fileStream, string fileName, ContentType contentType);
    Task<Stream?> GetFileAsync(string filePath);
    Task<bool> DeleteFileAsync(string filePath);
    string GetFilePath(string fileName);
}