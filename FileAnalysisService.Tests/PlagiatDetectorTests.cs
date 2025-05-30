using FileAnalysisService.Data;
using FileAnalysisService.Results;
using FileAnalysisService.Services.PlagiatDetector;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

public class PlagiatDetectorTests
{
    [Fact]
    public async Task CheckPlagiatAsync_ShouldReturnFalseIfNoMatch()
    {
        var options = new DbContextOptionsBuilder<FileAnalysisDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new FileAnalysisDbContext(options);

        var logger = Mock.Of<ILogger<PlagiatDetector>>();
        var detector = new PlagiatDetector(context, logger);

        var result = await detector.CheckPlagiatAsync(Guid.NewGuid(), new MemoryStream(), "somehash");

        Assert.False(result.IsPlagiat);
    }
}