namespace FileStoringService.DTOs;

using System;

public class FileMetadataDto
{
    /// <summary>
    /// Уникальный идентификатор файла (GUID).
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Оригинальное имя файла, как его загрузил пользователь.
    /// </summary>
    public string OriginalFileName { get; set; }

    /// <summary>
    /// MIME-тип файла (например, "text/plain").
    /// </summary>
    public string ContentType { get; set; }

    /// <summary>
    /// Размер файла в байтах.
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    /// Время (UTC) загрузки файла.
    /// </summary>
    public DateTime UploadedAt { get; set; }
}