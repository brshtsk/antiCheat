using FileAnalysisService.Results;

namespace FileAnalysisService.Services.Orchestrator;

public interface IFileAnalysisOrchestrator
{
    Task<AnalysisResult> AnalyzeFileAsync(Guid fileId, string fileHash);
}