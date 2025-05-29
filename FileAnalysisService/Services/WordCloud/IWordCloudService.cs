using FileAnalysisService.DTOs;

namespace FileAnalysisService.Services.WordCloud;

public interface IWordCloudService
{
    Task<WordCloudResultDto> GenerateAndSaveWordCloudAsync(Guid fileId, Stream stream);
}