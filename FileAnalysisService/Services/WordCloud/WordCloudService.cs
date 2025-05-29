using System.Text;
using System.Threading.Tasks;
using FileAnalysisService.Clients.WordCloud;
using FileAnalysisService.DTOs;
using FileAnalysisService.Services.FileStorage;
using Microsoft.Extensions.Logging;

namespace FileAnalysisService.Services.WordCloud
{
    public class WordCloudService : IWordCloudService
    {
        private readonly IWordCloudClient _wordCloudClient;
        private readonly IFileStorageProvider _fileStorageProvider;
        private readonly ILogger<WordCloudService> _logger;

        public WordCloudService(
            IWordCloudClient wordCloudClient,
            IFileStorageProvider fileStorageProvider,
            ILogger<WordCloudService> logger)
        {
            _wordCloudClient = wordCloudClient;
            _fileStorageProvider = fileStorageProvider;
            _logger = logger;
        }

        public async Task<WordCloudResultDto> GenerateAndSaveWordCloudAsync(Guid fileId, Stream stream)
        {
            if (fileId == Guid.Empty)
                return Error("FileId не может быть пустым.");

            if (stream == null || !stream.CanRead)
                return Error("Входной поток недоступен для чтения.");

            string textContent;
            try
            {
                using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true,
                    leaveOpen: true);
                textContent = await reader.ReadToEndAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при чтении текста из потока для FileId: {FileId}", fileId);
                return Error("Не удалось прочитать текст из файла.");
            }

            if (string.IsNullOrWhiteSpace(textContent))
            {
                _logger.LogWarning("Пустой текстовый контент для FileId: {FileId}", fileId);
                return Error("Файл не содержит текста.");
            }

            try
            {
                var preview = textContent.Length <= 50
                    ? textContent
                    : textContent.Substring(0, 50);
                _logger.LogInformation(
                    "Генерация облака слов для FileId: {FileId}, текст: '{Preview}...'",
                    fileId,
                    preview);

                var parameters = new ImageSpecs
                {
                    Width = 800,
                    Height = 600,
                    BackgroundColor = "#FFFFFF",
                    FontFamily = "Arial",
                    FontSize = 20,
                    FontColor = "#000000",
                    FontScale = 1.5,
                    RemoveStopwords = true,
                    Language = "ru",
                    Format = "png",
                    UseWordList = false
                };

                using var imageStream = await _wordCloudClient.GenerateWordCloudAsync(textContent, parameters);
                if (imageStream == null || imageStream.Length == 0)
                {
                    _logger.LogWarning(
                        "WordCloud API вернул пустой поток или null для FileId: {FileId}",
                        fileId);
                    return Error("API не сгенерировало изображение.");
                }

                string fileName = $"{fileId}_wordcloud.{parameters.Format}";
                _logger.LogInformation(
                    "Сохранение изображения для FileId: {FileId}, имя: {FileName}",
                    fileId,
                    fileName);

                var savedPath = await _fileStorageProvider.SaveFileAsync(
                    imageStream,
                    fileName,
                    ContentType.Image);

                _logger.LogInformation(
                    "Изображение сохранено: {SavedPath} для FileId: {FileId}",
                    savedPath,
                    fileId);

                return new WordCloudResultDto
                {
                    Success = true,
                    ImageLocation = savedPath
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Ошибка генерации/сохранения облака слов для FileId: {FileId}",
                    fileId);
                return Error("Произошла ошибка при генерации облака слов.");
            }
        }

        private static WordCloudResultDto Error(string message) => new()
        {
            Success = false,
            ErrorMessage = message
        };
    }
}