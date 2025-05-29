namespace FileStoringService.DTOs;

public class FileInfoDto
{
    public Guid Id { get; set; }
    public string FileName { get; set; }
    public DateTime UploadedAt { get; set; }
}
