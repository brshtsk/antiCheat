using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FileAnalysisService.Results;

public enum AnalysisStatus
{
    Pending,
    InProgress,
    Completed,
    Failed
}

[Table("AnalysisResult")]
public class AnalysisResult
{
    [Key] public Guid Id { get; set; }

    [Required] public Guid FileId { get; set; }

    public int ParagraphCount { get; set; }
    public int WordCount { get; set; }
    public int CharCount { get; set; }

    [Column(TypeName = "jsonb")] public string? PlagiatData { get; set; }
    public string? WordCloudImagePath { get; set; }
    public AnalysisStatus Status { get; set; } = AnalysisStatus.Pending;
    public DateTime? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }

    [Required] [MaxLength(64)] public string FileHash { get; set; }
}