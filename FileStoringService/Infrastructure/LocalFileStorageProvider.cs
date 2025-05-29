using Microsoft.Extensions.Options;

namespace FileStoringService.Infrastructure;

public class LocalFileStorageProvider : IFileStorageProvider
{
    private readonly string _basePath;
    private readonly ILogger<LocalFileStorageProvider> _logger;

    public LocalFileStorageProvider(IOptions<FileStorageSettings> settings, ILogger<LocalFileStorageProvider> logger)
    {
        _logger = logger;
        _basePath = settings.Value.BasePath ?? throw new ArgumentNullException(nameof(settings.Value.BasePath),
            "File storage base path is not configured.");

        try
        {
            if (!Directory.Exists(_basePath))
            {
                Directory.CreateDirectory(_basePath);
                _logger.LogInformation("Storage directory created at: {BasePath}", _basePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create storage directory at: {BasePath}", _basePath);
            throw;
        }
    }

    public async Task<string> SaveFileAsync(Stream stream, string originalFileName, string contentType)
    {
        var fileExtension = Path.GetExtension(originalFileName);
        var storedFileName = $"{Guid.NewGuid()}{fileExtension}";
        var filePath = Path.Combine(_basePath, storedFileName);

        try
        {
            _logger.LogInformation("Attempting to save file to: {FilePath}", filePath);
            stream.Position = 0;
            using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write))
            {
                await stream.CopyToAsync(fileStream);
            }

            _logger.LogInformation("Successfully saved file: {StoredFileName} from original: {OriginalFileName}",
                storedFileName, originalFileName);
            return storedFileName;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save file: {OriginalFileName} to path: {FilePath}", originalFileName,
                filePath);
            throw;
        }
    }

    public Task<Stream> GetFileAsync(string storedFileName)
    {
        var filePath = Path.Combine(_basePath, storedFileName);
        _logger.LogInformation("Attempting to retrieve file from: {FilePath}", filePath);

        if (!File.Exists(filePath))
        {
            _logger.LogWarning("File not found at: {FilePath}", filePath);
            throw new FileNotFoundException("File not found in storage.", storedFileName);
        }

        return Task.FromResult<Stream>(new FileStream(filePath, FileMode.Open, FileAccess.Read,
            FileShare.Read)); // Добавил FileShare.Read
    }

    public Task<bool> DeleteAsync(string storedFileName)
    {
        var filePath = Path.Combine(_basePath, storedFileName);
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                _logger.LogInformation("Successfully deleted file: {StoredFileName}", storedFileName);
                return Task.FromResult(true);
            }

            _logger.LogWarning("Attempted to delete non-existent file: {StoredFileName}", storedFileName);
            return Task.FromResult(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete file: {StoredFileName}", storedFileName);
            return Task.FromResult(false);
        }
    }

    public string GetFilePath(string storedFileName)
    {
        return Path.Combine(_basePath, storedFileName);
    }
}