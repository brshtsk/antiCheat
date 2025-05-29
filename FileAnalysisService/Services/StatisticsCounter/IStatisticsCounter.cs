namespace FileAnalysisService.Services.StatisticsCounter;

public class Statistics
{
    public int WordCount { get; set; }
    public int CharacterCount { get; set; }
    public int ParagraphCount { get; set; }
}

public interface IStatisticsCounter
{
    Task<Statistics> CountStatisticsAsync(Stream textStream);
}