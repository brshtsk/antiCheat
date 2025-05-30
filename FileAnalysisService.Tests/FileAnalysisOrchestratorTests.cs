using FileAnalysisService.Services.Orchestrator;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using System;
using System.Threading.Tasks;
using FileAnalysisService.Data;
using FileAnalysisService.Results;
using FileAnalysisService.Clients.FileStoring;
using FileAnalysisService.Services.StatisticsCounter;
using FileAnalysisService.Services.PlagiatDetector;
using FileAnalysisService.Services.WordCloud;
using FileAnalysisService.DTOs;
using Microsoft.EntityFrameworkCore;

public class FileAnalysisOrchestratorTests
{
    [Fact]
    public async Task AnalyzeFileAsync_ReturnsCompletedStatus()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<FileAnalysisDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var context = new FileAnalysisDbContext(options);

        var fileId = Guid.NewGuid();
        var fileData = new FileData { Content = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("hello test")) };

        var fileClient = new Mock<IFileStoringServiceClient>();
        fileClient.Setup(x => x.GetFileDataAsync(fileId)).ReturnsAsync(fileData);

        var statCounter = new Mock<IStatisticsCounter>();
        statCounter.Setup(x => x.CountStatisticsAsync(It.IsAny<Stream>()))
            .ReturnsAsync(new Statistics { WordCount = 2, CharacterCount = 10, ParagraphCount = 1 });

        var plagiatDetector = new Mock<IPlagiatDetector>();
        plagiatDetector.Setup(x => x.CheckPlagiatAsync(fileId, It.IsAny<Stream>(), It.IsAny<string>()))
            .ReturnsAsync(new PlagiatData { IsPlagiat = false });

        var wordCloudService = new Mock<IWordCloudService>();
        wordCloudService.Setup(x => x.GenerateAndSaveWordCloudAsync(fileId, It.IsAny<Stream>()))
            .Returns(Task.FromResult(new WordCloudResultDto { Success = true, ImageLocation = "test.png" }));


        var logger = Mock.Of<ILogger<FileAnalysisOrchestrator>>();

        var orchestrator = new FileAnalysisOrchestrator(context, fileClient.Object, statCounter.Object,
            plagiatDetector.Object, wordCloudService.Object, logger);

        // Act
        var result = await orchestrator.AnalyzeFileAsync(fileId, "hash");

        // Assert
        Assert.Equal(AnalysisStatus.Completed, result.Status);
    }
}