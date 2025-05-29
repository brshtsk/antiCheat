namespace FileStoringService.Infrastructure;

public interface IFileStorageProvider
{
    /// <summary>
    /// Сохраняет поток в хранилище и возвращает внутреннее (уникальное) имя файла.
    /// </summary>
    Task<string> SaveFileAsync(Stream fileStream, string originalFileName, string contentType);

    /// <summary>
    /// Открывает файл по внутреннему имени.
    /// </summary>
    Task<Stream> GetFileAsync(string storedFileName);

    /// <summary>
    /// Удаляет файл по внутреннему имени.
    /// </summary>
    Task<bool> DeleteAsync(string storedFileName);

    /// <summary>
    /// Получает полный путь к файлу по его внутреннему имени.
    /// </summary>
    string GetFilePath(string storedFileName);
}