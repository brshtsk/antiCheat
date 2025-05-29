using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using FileAnalysisService.Data;
using FileAnalysisService.DTOs;
using FileAnalysisService.Results;
using FileAnalysisService.Services;
using FileAnalysisService.Services.FileStorage;
using FileAnalysisService.Services.Orchestrator;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using FileAnalysisService.Clients.FileStoring;

namespace FileAnalysisService.Controllers;

[Route("analysis")]
[ApiController]
public class AnalysisController : ControllerBase
{
    private readonly IFileStoringServiceClient _storingClient;
    private readonly IFileAnalysisOrchestrator _analysisOrchestrator;
    private readonly FileAnalysisDbContext _db;
    private readonly IFileStorageProvider _storageProvider;
    private readonly ILogger<AnalysisController> _logger;

    public AnalysisController(
        IFileStoringServiceClient storingClient,
        IFileAnalysisOrchestrator analysisOrchestrator,
        FileAnalysisDbContext db,
        IFileStorageProvider storageProvider,
        ILogger<AnalysisController> logger)
    {
        _storingClient = storingClient;
        _analysisOrchestrator = analysisOrchestrator;
        _db = db;
        _storageProvider = storageProvider;
        _logger = logger;
    }

    /// <summary>
    /// Запрос на анализ файла: вычисляем хэш, проверяем уникальность, запускаем или возвращаем существующий.
    /// </summary>
    [HttpPost("{fileId:guid}")]
    [ProducesResponseType(typeof(AnalysisResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> RequestAnalysis(Guid fileId)
    {
        if (fileId == Guid.Empty)
        {
            return BadRequest("FileId is required.");
        }

        _logger.LogInformation("Starting analysis request for FileId={FileId}", fileId);

        // 1. Получаем файл «на лету» у FileStoringService
        FileData? fileData;
        try
        {
            fileData = await _storingClient.GetFileDataAsync(fileId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch file {FileId} from FileStoringService", fileId);
            return StatusCode(500, "Unable to fetch file for analysis.");
        }

        if (fileData == null)
        {
            _logger.LogWarning("File {FileId} not found in FileStoringService", fileId);
            return NotFound("Source file not found.");
        }

        // Используем напрямую поток из HTTP-ответа
        using var fileStream = fileData.Content;


        // 2. Вычисляем SHA-256 хэш содержимого
        string hash;
        try
        {
            // fileStream.Position = 0;
            using var sha256 = SHA256.Create();
            var contentBytes = await ReadAllBytesAsync(fileStream);
            hash = Convert.ToHexString(sha256.ComputeHash(contentBytes));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to compute hash for FileId={FileId}", fileId);
            return StatusCode(500, "Error computing file hash.");
        }

        _logger.LogInformation("Computed hash for FileId={FileId}: {Hash}", fileId, hash);

        // 3. Проверяем, есть ли уже анализ с таким же FileId и хэшем
        var existing = await _db.AnalysisResults
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.FileId == fileId && r.FileHash == hash);

        if (existing != null)
        {
            _logger.LogInformation(
                "FileId={FileId} with same hash already analyzed (AnalysisId={AnalysisId})",
                fileId, existing.Id);
            return Conflict(
                $"File with identical content was already analyzed at {existing.CompletedAt}.");
        }

        // 4. Запускаем новый анализ
        AnalysisResult result;
        try
        {
            result = await _analysisOrchestrator.AnalyzeFileAsync(fileId, hash);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error during analysis orchestration for FileId={FileId}",
                fileId);
            return StatusCode(500, "Analysis failed.");
        }

        var dto = MapToDto(result);

        if (result.Status == AnalysisStatus.Completed)
        {
            return Ok(dto);
        }

        // Если анализ в процессе или на очереди
        return AcceptedAtAction(
            nameof(GetAnalysisResultByFileId),
            new { fileId },
            dto);
    }

    [HttpGet("file/{fileId:guid}")]
    [ProducesResponseType(typeof(AnalysisResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAnalysisResultByFileId(Guid fileId)
    {
        if (fileId == Guid.Empty)
        {
            return BadRequest("FileId is required.");
        }

        _logger.LogInformation("Fetching analysis result for FileId={FileId}", fileId);

        var result = await _db.AnalysisResults
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.FileId == fileId);

        if (result == null)
        {
            _logger.LogWarning(
                "Analysis result not found for FileId={FileId}",
                fileId);
            return NotFound();
        }

        return Ok(MapToDto(result));
    }

    [HttpGet("byAnalysisId/{analysisId:guid}")]
    [ProducesResponseType(typeof(AnalysisResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAnalysisResultById(Guid analysisId)
    {
        if (analysisId == Guid.Empty)
        {
            return BadRequest("AnalysisId is required.");
        }

        _logger.LogInformation(
            "Fetching analysis result for AnalysisId={AnalysisId}",
            analysisId);

        var result = await _db.AnalysisResults
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == analysisId);

        if (result == null)
        {
            _logger.LogWarning(
                "Analysis result not found for AnalysisId={AnalysisId}",
                analysisId);
            return NotFound();
        }

        return Ok(MapToDto(result));
    }

    [HttpGet("wordcloud/file/{fileId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetWordCloudImageByFileId(Guid fileId)
    {
        if (fileId == Guid.Empty)
        {
            return BadRequest("FileId is required.");
        }

        var result = await _db.AnalysisResults
            .AsNoTracking()
            .FirstOrDefaultAsync(
                r => r.FileId == fileId && r.Status == AnalysisStatus.Completed);

        if (result == null || string.IsNullOrEmpty(result.WordCloudImagePath))
        {
            return NotFound();
        }

        try
        {
            var stream = await _storageProvider.GetFileAsync(
                result.WordCloudImagePath);
            var contentType = DetermineContentType(
                result.WordCloudImagePath);
            return File(
                stream,
                contentType,
                Path.GetFileName(result.WordCloudImagePath));
        }
        catch (FileNotFoundException ex)
        {
            _logger.LogError(
                ex,
                "Word cloud image not found for FileId={FileId} at {Location}",
                fileId,
                result.WordCloudImagePath);
            return NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error streaming word cloud image for FileId={FileId}", fileId);
            return StatusCode(500);
        }
    }

    private static async Task<byte[]> ReadAllBytesAsync(Stream stream)
    {
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);
        return ms.ToArray();
    }

    private static string DetermineContentType(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".svg" => "image/svg+xml",
            _ => "application/octet-stream",
        };
    }

    private AnalysisResultDto MapToDto(AnalysisResult result)
    {
        return new AnalysisResultDto
        {
            AnalysisId = result.Id,
            FileId = result.FileId,

            // Мапим хэш
            FileHash = result.FileHash,

            ParagraphCount = result.ParagraphCount,
            WordCount = result.WordCount,
            CharacterCount = result.CharCount,
            PlagiatData = result.PlagiatData,
            WordCloudImagePath = result.WordCloudImagePath,
            AnalysisStatus = result.Status.ToString(),
            CompletedAt = result.CompletedAt,
            ErrorMessage = result.ErrorMessage
        };
    }
}