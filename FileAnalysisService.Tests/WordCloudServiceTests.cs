using FileAnalysisService.Services.WordCloud;
using FileAnalysisService.Clients.WordCloud;
using FileAnalysisService.Services.FileStorage;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Xunit;

public class WordCloudServiceTests
{
    [Fact]
    public async Task GenerateAndSaveWordCloudAsync_ShouldReturnSuccess()
    {
        var fileId = Guid.NewGuid();
        var inputText = "word cloud test";

        var wordCloudClient = new Mock<IWordCloudClient>();
        wordCloudClient.Setup(x => x.GenerateWordCloudAsync(It.IsAny<string>(), It.IsAny<ImageSpecs>()))
            .ReturnsAsync(new MemoryStream(Encoding.UTF8.GetBytes("image data")));

        var fileStorage = new Mock<IFileStorageProvider>();
        fileStorage.Setup(x => x.SaveFileAsync(It.IsAny<Stream>(), It.IsAny<string>(), ContentType.Image))
            .ReturnsAsync("saved/path/image.png");

        var logger = Mock.Of<ILogger<WordCloudService>>();

        var service = new WordCloudService(wordCloudClient.Object, fileStorage.Object, logger);
        var result =
            await service.GenerateAndSaveWordCloudAsync(fileId, new MemoryStream(Encoding.UTF8.GetBytes(inputText)));

        Assert.True(result.Success);
        Assert.Equal("saved/path/image.png", result.ImageLocation);
    }
}