namespace FileAnalysisService.DTOs;

public class WordCloudResultDto
{
    public bool Success { get; set; }
    public string? ImageLocation { get; set; } 
    public string? ErrorMessage { get; set; }
}