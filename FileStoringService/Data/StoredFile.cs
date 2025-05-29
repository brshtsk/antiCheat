namespace FileStoringService.Data;

using System;

public class StoredFile
{
    /// <summary>
    /// Первичный ключ — GUID.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Оригинальное имя файла (у себя на компьютере у пользователя).
    /// </summary>
    public string OriginalFileName { get; set; }

    /// <summary>
    /// MIME-тип (например, "text/plain").
    /// </summary>
    public string ContentType { get; set; }

    /// <summary>
    /// Внутреннее имя или путь на диске (Location).
    /// Мы будем сохранять файлы в FileStorage.BasePath/{StoredFileName}.
    /// </summary>
    public string StoredFileName { get; set; }

    /// <summary>
    /// Размер файла в байтах.
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    /// Время загрузки (UTC).
    /// </summary>
    public DateTime UploadedAt { get; set; }
}