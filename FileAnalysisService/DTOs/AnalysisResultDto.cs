namespace FileAnalysisService.DTOs;

public class AnalysisResultDto
{
    public Guid AnalysisId { get; set; }
    public Guid FileId { get; set; }
    public string FileContentHash { get; set; } // вот это
    public int ParagraphCount { get; set; }
    public int WordCount { get; set; }
    public int CharCount { get; set; }
    public string? PlagiarismScores { get; set; }
    public string? WordCloudImageLocation { get; set; }
    public string Status { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }
}