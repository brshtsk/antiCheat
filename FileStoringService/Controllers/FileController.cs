using FileStoringService.Data;
using FileStoringService.Infrastructure;
using FileStoringService.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FileStoringService.Controllers;

[Route("files")]
[ApiController]
public class FilesController : ControllerBase
{
    private readonly FileStoringDbContext _context;
    private readonly IFileStorageProvider _fileStorageProvider;
    private readonly ILogger<FilesController> _logger;

    public FilesController(
        FileStoringDbContext context,
        IFileStorageProvider fileStorageProvider,
        ILogger<FilesController> logger)
    {
        _context = context;
        _fileStorageProvider = fileStorageProvider;
        _logger = logger;
    }

    [HttpPost("upload")]
    [ProducesResponseType(typeof(FileMetadataDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UploadFile(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            _logger.LogWarning("UploadFile called with no file or empty file.");
            return BadRequest("File is not provided or empty.");
        }

        if (Path.GetExtension(file.FileName).ToLowerInvariant() != ".txt")
        {
            _logger.LogWarning("UploadFile called with non-.txt file: {FileName}", file.FileName);
            return BadRequest("Only .txt files are allowed.");
        }

        _logger.LogInformation("UploadFile called for file: {FileName}, Size: {FileSize}", file.FileName, file.Length);

        try
        {
            // Сохраняем файл в хранилище
            await using var stream = file.OpenReadStream();
            var storedFileNameOnDisk = await _fileStorageProvider.SaveFileAsync(
                stream,
                file.FileName,
                file.ContentType);

            // Создаём запись в базе
            var newStoredFile = new StoredFile
            {
                Id = Guid.NewGuid(),
                OriginalFileName = file.FileName,
                ContentType = file.ContentType,
                StoredFileName = storedFileNameOnDisk,
                FileSize = file.Length,
                UploadedAt = DateTime.UtcNow
            };

            _context.StoredFiles.Add(newStoredFile);
            await _context.SaveChangesAsync();

            _logger.LogInformation("New file stored with ID: {FileId}, Original name: {OriginalFileName}",
                newStoredFile.Id, newStoredFile.OriginalFileName);

            var dto = new FileMetadataDto
            {
                Id = newStoredFile.Id,
                OriginalFileName = newStoredFile.OriginalFileName,
                ContentType = newStoredFile.ContentType,
                FileSize = newStoredFile.FileSize,
                UploadedAt = newStoredFile.UploadedAt
                // Поле Hash здесь опущено, поскольку дублирование не проверяем
            };

            return CreatedAtAction(
                nameof(GetFileMetadata),
                new { id = newStoredFile.Id },
                dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during file upload for {FileName}.", file.FileName);
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                "An internal server error occurred.");
        }
    }

    [HttpGet("{id}/metadata")]
    [ProducesResponseType(typeof(FileMetadataDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetFileMetadata(Guid id)
    {
        _logger.LogInformation("GetFileMetadata called for ID: {FileId}", id);

        var storedFile = await _context.StoredFiles
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == id);

        if (storedFile == null)
        {
            _logger.LogWarning("File metadata not found for ID: {FileId}", id);
            return NotFound();
        }

        return Ok(new FileMetadataDto
        {
            Id = storedFile.Id,
            OriginalFileName = storedFile.OriginalFileName,
            ContentType = storedFile.ContentType,
            FileSize = storedFile.FileSize,
            UploadedAt = storedFile.UploadedAt
            // Hash не возвращаем
        });
    }

    [HttpGet("{id}/download")]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DownloadFile(Guid id)
    {
        _logger.LogInformation("DownloadFile called for ID: {FileId}", id);

        var storedFile = await _context.StoredFiles
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == id);

        if (storedFile == null)
        {
            _logger.LogWarning("File metadata not found for download, ID: {FileId}", id);
            return NotFound("File metadata not found.");
        }

        try
        {
            var fileStream = await _fileStorageProvider.GetFileAsync(storedFile.StoredFileName);
            _logger.LogInformation(
                "Streaming file {OriginalFileName} (ID: {FileId}) for download.",
                storedFile.OriginalFileName, id);

            return File(
                fileStream,
                storedFile.ContentType,
                storedFile.OriginalFileName);
        }
        catch (FileNotFoundException ex)
        {
            _logger.LogError(
                ex,
                "Physical file not found for StoredFileName: {StoredFileName}, ID: {FileId}",
                storedFile.StoredFileName, id);

            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error occurred during file download for ID: {FileId}", id);

            return StatusCode(
                StatusCodes.Status500InternalServerError,
                "An internal server error occurred while retrieving the file.");
        }
    }
}