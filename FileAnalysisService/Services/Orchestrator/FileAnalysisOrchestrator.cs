using FileAnalysisService.Clients.FileStoring;
using FileAnalysisService.Data;
using FileAnalysisService.Results;
using FileAnalysisService.Services.PlagiatDetector;
using Microsoft.EntityFrameworkCore;
using FileAnalysisService.Services.StatisticsCounter;
using FileAnalysisService.Services.WordCloud;

namespace FileAnalysisService.Services.Orchestrator;

public class FileAnalysisOrchestrator : IFileAnalysisOrchestrator
{
    private readonly FileAnalysisDbContext _dbContext;
    private readonly IFileStoringServiceClient _fileStoringServiceClient;
    private readonly IStatisticsCounter _textStatisticsService;
    private readonly IPlagiatDetector _plagiarismDetectionService;
    private readonly IWordCloudService _wordCloudGenerationService;
    private readonly ILogger<FileAnalysisOrchestrator> _logger;

    public FileAnalysisOrchestrator(
        FileAnalysisDbContext dbContext,
        IFileStoringServiceClient fileStoringServiceClient,
        IStatisticsCounter textStatisticsService,
        IPlagiatDetector plagiarismDetectionService,
        IWordCloudService wordCloudGenerationService,
        ILogger<FileAnalysisOrchestrator> logger)
    {
        _dbContext = dbContext;
        _fileStoringServiceClient = fileStoringServiceClient;
        _textStatisticsService = textStatisticsService;
        _plagiarismDetectionService = plagiarismDetectionService;
        _wordCloudGenerationService = wordCloudGenerationService;
        _logger = logger;
    }

    public async Task<AnalysisResult> AnalyzeFileAsync(Guid fileId, string fileHash)
    {
        _logger.LogInformation("Starting analysis for FileId: {FileId}, Hash: {FileHash}", fileId, fileHash);

        var existingResult = await _dbContext.AnalysisResults
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.FileId == fileId && r.Status == AnalysisStatus.Completed);
        if (existingResult != null)
        {
            _logger.LogInformation(
                "Analysis for FileId: {FileId} already exists and is completed. Returning existing result.", fileId);
            return existingResult;
        }

        var analysisResult = await _dbContext.AnalysisResults
            .FirstOrDefaultAsync(r => r.FileId == fileId);

        if (analysisResult == null)
        {
            analysisResult = new AnalysisResult
            {
                Id = Guid.NewGuid(),
                FileId = fileId,
                FileHash = fileHash,
                Status = AnalysisStatus.InProgress,
            };
            _dbContext.AnalysisResults.Add(analysisResult);
        }
        else
        {
            analysisResult.Status = AnalysisStatus.InProgress;
            analysisResult.ErrorMessage = null;
            analysisResult.FileHash = fileHash;
        }

        await _dbContext.SaveChangesAsync();

        try
        {
            _logger.LogDebug("Fetching file content for FileId: {FileId} from FileStoringService.", fileId);
            var fileData = await _fileStoringServiceClient.GetFileDataAsync(fileId);

            if (fileData == null || fileData.Content == null)
            {
                _logger.LogError("Failed to retrieve file content for FileId: {FileId} from FileStoringService.",
                    fileId);
                analysisResult.Status = AnalysisStatus.Failed;
                analysisResult.ErrorMessage = "Failed to retrieve file content from storage.";
                await _dbContext.SaveChangesAsync();
                return analysisResult;
            }

            using (var networkStream = fileData.Content)
            using (var memoryStream = new MemoryStream())
            {
                await networkStream.CopyToAsync(memoryStream);
                memoryStream.Position = 0;
                _logger.LogDebug("Calculating text statistics for FileId: {FileId}.", fileId);
                var statistics = await _textStatisticsService.CountStatisticsAsync(memoryStream);
                analysisResult.ParagraphCount = statistics.ParagraphCount;
                analysisResult.WordCount = statistics.WordCount;
                analysisResult.CharCount = statistics.CharacterCount;
                _logger.LogInformation("Text statistics for FileId: {FileId} - P: {P}, W: {W}, C: {C}",
                    fileId, statistics.ParagraphCount, statistics.WordCount, statistics.CharacterCount);

                memoryStream.Position = 0;
                _logger.LogDebug("Detecting plagiarism for FileId: {FileId}.", fileId);
                var plagiarism =
                    await _plagiarismDetectionService.CheckPlagiatAsync(fileId, memoryStream, fileHash);
                analysisResult.PlagiatData = System.Text.Json.JsonSerializer.Serialize(plagiarism);
                _logger.LogInformation(
                    "Plagiarism check for FileId: {FileId} - IsFull: {IsFull}, OriginalId: {OriginalId}",
                    fileId, plagiarism.IsPlagiat, plagiarism.OriginalFileId);

                if (_wordCloudGenerationService != null)
                {
                    memoryStream.Position = 0;
                    _logger.LogDebug("Generating word cloud for FileId: {FileId}.", fileId);
                    var wordCloudResult =
                        await _wordCloudGenerationService.GenerateAndSaveWordCloudAsync(fileId, memoryStream);
                    if (wordCloudResult.Success)
                    {
                        analysisResult.WordCloudImagePath = wordCloudResult.ImageLocation;
                        _logger.LogInformation("Word cloud generated for FileId: {FileId}, Location: {Location}",
                            fileId, wordCloudResult.ImageLocation);
                    }
                    else
                    {
                        _logger.LogWarning("Failed to generate word cloud for FileId: {FileId}. Error: {Error}", fileId,
                            wordCloudResult.ErrorMessage);
                    }
                }
            }

            analysisResult.Status = AnalysisStatus.Completed;
            analysisResult.CompletedAt = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred during analysis of FileId: {FileId}", fileId);
            analysisResult.Status = AnalysisStatus.Failed;
            analysisResult.ErrorMessage = ex.Message;
        }

        await _dbContext.SaveChangesAsync();
        _logger.LogInformation("Analysis finished for FileId: {FileId} with Status: {Status}", fileId,
            analysisResult.Status);
        return analysisResult;
    }
}