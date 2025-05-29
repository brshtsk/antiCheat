namespace FileAnalysisService.DTOs;

public class AnalysisResultDto
{
    public Guid AnalysisId { get; set; }
    public Guid FileId { get; set; }
    public string FileHash { get; set; } // вот это
    public int ParagraphCount { get; set; }
    public int WordCount { get; set; }
    public int CharacterCount { get; set; }
    public string? PlagiatData { get; set; }
    public string? WordCloudImagePath { get; set; }
    public string AnalysisStatus { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }
}