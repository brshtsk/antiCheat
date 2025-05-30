using FileAnalysisService.Services.StatisticsCounter;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Xunit;

public class StatisticsCounterTests
{
    [Fact]
    public async Task CountStatisticsAsync_ReturnsCorrectCounts()
    {
        var input = "This is a test.\n\nAnother paragraph.";
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(input));

        var counter = new StatisticsCounter();
        var stats = await counter.CountStatisticsAsync(stream);

        Assert.Equal(2, stats.ParagraphCount);
        Assert.Equal(6, stats.WordCount);
        Assert.Equal(input.Length, stats.CharacterCount);
    }
}