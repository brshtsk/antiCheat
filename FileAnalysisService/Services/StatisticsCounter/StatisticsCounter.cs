namespace FileAnalysisService.Services.StatisticsCounter;

public class StatisticsCounter : IStatisticsCounter
{
    public async Task<Statistics> CountStatisticsAsync(Stream textStream)
    {
        if (textStream == null || textStream.Length == 0)
        {
            return new Statistics
            {
                WordCount = 0,
                CharacterCount = 0,
                ParagraphCount = 0
            };
        }

        using var reader = new StreamReader(textStream);
        var content = await reader.ReadToEndAsync();

        var wordCount = content.Split(new[] { ' ', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Length;
        var characterCount = content.Length;
        var paragraphCount = content.Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries).Length;

        return new Statistics
        {
            WordCount = wordCount,
            CharacterCount = characterCount,
            ParagraphCount = paragraphCount
        };
    }
}