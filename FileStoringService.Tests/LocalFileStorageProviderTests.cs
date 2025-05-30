using System;
using System.IO;
using System.Threading.Tasks;
using FileStoringService.Infrastructure;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FileStoringService.Tests
{
    public class LocalFileStorageProviderTests : IDisposable
    {
        private readonly string _basePath;
        private readonly LocalFileStorageProvider _provider;

        public LocalFileStorageProviderTests()
        {
            // создаём уникальную временную папку
            _basePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            var options = Options.Create(new FileStorageSettings { BasePath = _basePath });
            var logger = Mock.Of<ILogger<LocalFileStorageProvider>>();
            _provider = new LocalFileStorageProvider(options, logger);
        }

        public void Dispose()
        {
            // чистим временную папку
            if (Directory.Exists(_basePath))
                Directory.Delete(_basePath, recursive: true);
        }

        [Fact]
        public async Task SaveFileAsync_ShouldSaveAndReturnFilenameWithSameExtension()
        {
            // Arrange
            var content = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("hello"));
            const string original = "test.txt";

            // Act
            var stored = await _provider.SaveFileAsync(content, original, "text/plain");

            // Assert
            Assert.EndsWith(".txt", stored);
            var full = Path.Combine(_basePath, stored);
            Assert.True(File.Exists(full));
        }

        [Fact]
        public async Task GetFileAsync_ExistingFile_ShouldReturnStream()
        {
            // Arrange
            var content = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("data"));
            var stored = await _provider.SaveFileAsync(content, "x.bin", "application/octet-stream");

            // Act
            using var stream = await _provider.GetFileAsync(stored);
            using var reader = new StreamReader(stream);
            var text = await reader.ReadToEndAsync();

            // Assert
            Assert.Equal("data", text);
        }

        [Fact]
        public async Task GetFileAsync_NonExisting_ShouldThrowFileNotFoundException()
        {
            await Assert.ThrowsAsync<FileNotFoundException>(
                () => _provider.GetFileAsync("does-not-exist.dat"));
        }

        [Fact]
        public async Task DeleteAsync_ExistingFile_ReturnsTrueAndDeletes()
        {
            // Arrange
            var content = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("1"));
            var stored = await _provider.SaveFileAsync(content, "a.txt", "text/plain");
            var full = Path.Combine(_basePath, stored);
            Assert.True(File.Exists(full));

            // Act
            var result = await _provider.DeleteAsync(stored);

            // Assert
            Assert.True(result);
            Assert.False(File.Exists(full));
        }

        [Fact]
        public async Task DeleteAsync_NonExistingFile_ReturnsFalse()
        {
            var result = await _provider.DeleteAsync("nope.png");
            Assert.False(result);
        }

        [Fact]
        public void GetFilePath_ReturnsCorrectCombination()
        {
            var combined = _provider.GetFilePath("foo.bar");
            Assert.Equal(Path.Combine(_basePath, "foo.bar"), combined);
        }
    }
}
