using System;
using System.IO;
using System.Threading.Tasks;
using FileAnalysisService.Data;
using FileAnalysisService.Services.PlagiatDetector;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FileAnalysisService.Services.PlagiatDetector;

public class PlagiatDetector : IPlagiatDetector
{
    private readonly FileAnalysisDbContext _db;
    private readonly ILogger<PlagiatDetector> _logger;

    public PlagiatDetector(FileAnalysisDbContext db, ILogger<PlagiatDetector> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<PlagiatData> CheckPlagiatAsync(Guid fileId, Stream stream, string hash)
    {
        // Поскольку хэш уже рассчитан, можем проигнорировать stream.
        try
        {
            // Ищем в БД существующий результат анализа с тем же хэшем, но другим FileId
            var existing = await _db.AnalysisResults
                .AsNoTracking()
                .FirstOrDefaultAsync(r =>
                    r.FileHash == hash && r.FileId != fileId &&
                    r.Status == Results.AnalysisStatus.Completed);

            if (existing != null)
            {
                _logger.LogInformation(
                    "Plagiat detected: file {FileId} matches hash of previously analyzed file {ExistingFileId}",
                    fileId, existing.FileId);

                return new PlagiatData
                {
                    IsPlagiat = true,
                    OriginalFileHash = hash,
                    OriginalFileId = existing.FileId
                };
            }

            return new PlagiatData { IsPlagiat = false };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during plagiarism check for FileId={FileId}", fileId);
            // В случае ошибки считаем, что плагиат не выявлен, но можно расширить логику обработки
            return new PlagiatData { IsPlagiat = false };
        }
    }
}