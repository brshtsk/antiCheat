using FileAnalysisService.Services.FileStorage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

public class FileStorageProviderTests
{
    [Fact]
    public async Task SaveFileAsync_ShouldSaveFileAndReturnFilename()
    {
        // Arrange
        var basePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var options = Options.Create(new FileStorageSettings { BasePath = basePath });
        var logger = Mock.Of<ILogger<LocalFileStorageProvider>>();
        var provider = new LocalFileStorageProvider(options, logger);

        var fileContent = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("Hello, world!"));

        // Act
        var fileName = await provider.SaveFileAsync(fileContent, "test.txt", ContentType.Text);

        // Assert
        Assert.True(File.Exists(Path.Combine(basePath, fileName)));
    }
}