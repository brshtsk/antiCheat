using System;
using System.IO;
using System.Threading.Tasks;
using FileAnalysisService.Services.FileStorage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FileAnalysisService.Services.FileStorage;

public class FileStorageSettings
{
    public string BasePath { get; set; } = null!;
}

public class LocalFileStorageProvider : IFileStorageProvider
{
    private readonly string _basePath;
    private readonly ILogger<LocalFileStorageProvider> _logger;

    public LocalFileStorageProvider(
        IOptions<FileStorageSettings> settings,
        ILogger<LocalFileStorageProvider> logger)
    {
        if (settings?.Value?.BasePath == null)
            throw new ArgumentNullException(
                nameof(settings),
                "FileStorageSettings.BasePath must be configured.");

        _basePath = settings.Value.BasePath;
        _logger = logger;

        try
        {
            if (!Directory.Exists(_basePath))
            {
                Directory.CreateDirectory(_basePath);
                _logger.LogInformation("Created storage directory at '{BasePath}'", _basePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create storage directory at '{BasePath}'", _basePath);
            throw;
        }
    }

    public async Task<string> SaveFileAsync(Stream fileStream, string fileName, ContentType contentType)
    {
        // Generate a unique filename, preserving the extension
        var extension = Path.GetExtension(fileName);
        var storedFileName = $"{Guid.NewGuid()}{extension}";
        var filePath = Path.Combine(_basePath, storedFileName);

        try
        {
            _logger.LogInformation(
                "Saving {ContentType} to '{FilePath}' (original name: {Original})",
                contentType, filePath, fileName);

            fileStream.Position = 0;
            await using var outStream = new FileStream(
                filePath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None);
            await fileStream.CopyToAsync(outStream);

            _logger.LogInformation(
                "{ContentType} saved as '{Stored}'",
                contentType, storedFileName);

            return storedFileName;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to save {ContentType} (original name: {Original}) to '{FilePath}'",
                contentType, fileName, filePath);
            throw;
        }
    }

    public Task<Stream?> GetFileAsync(string filePath)
    {
        var fullPath = Path.Combine(_basePath, filePath);
        _logger.LogInformation("Retrieving file from '{FullPath}'", fullPath);

        if (!File.Exists(fullPath))
        {
            _logger.LogWarning("File not found at '{FullPath}'", fullPath);
            return Task.FromResult<Stream?>(null);
        }

        Stream stream = new FileStream(
            fullPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);
        return Task.FromResult<Stream?>(stream);
    }

    public Task<bool> DeleteFileAsync(string filePath)
    {
        var fullPath = Path.Combine(_basePath, filePath);

        try
        {
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
                _logger.LogInformation("Deleted file '{FilePath}'", fullPath);
                return Task.FromResult(true);
            }

            _logger.LogWarning("Attempted to delete non-existent file '{FilePath}'", fullPath);
            return Task.FromResult(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete file '{FilePath}'", fullPath);
            return Task.FromResult(false);
        }
    }

    public string GetFilePath(string fileName)
    {
        return Path.Combine(_basePath, fileName);
    }
}