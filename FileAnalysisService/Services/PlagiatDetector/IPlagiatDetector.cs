namespace FileAnalysisService.Services.PlagiatDetector;

public class PlagiatData
{
    public bool IsPlagiat { get; set; }
    public string? OriginalFileHash { get; set; }
    public Guid? OriginalFileId { get; set; }
}

public interface IPlagiatDetector
{
    Task<PlagiatData> CheckPlagiatAsync(Guid fileId, Stream stream, string hash);
}