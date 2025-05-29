namespace FileAnalysisService.Clients.FileStoring;

public class FileStoringServiceClient : IFileStoringServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<FileStoringServiceClient> _logger;
    private readonly string _fileStoringServiceBaseUrl;

    private class FileMetadataFromStorageDto
    {
        public Guid Id { get; set; }
        public string OriginalFileName { get; set; }
        public string ContentType { get; set; }
        public long FileSize { get; set; }
        public DateTime UploadedAt { get; set; }
        public string Hash { get; set; }
    }

    public async Task<FileData?> GetFileDataAsync(Guid fileId)
    {
        _logger.LogInformation("Requesting file data for FileId: {FileId} from FileStoringService", fileId);

        var metadataUrl = $"files/{fileId}/metadata";

        HttpResponseMessage metadataResponse;
        try
        {
            _logger.LogDebug("Requesting metadata from: {BaseAddress}{MetadataUrl}", _httpClient.BaseAddress,
                metadataUrl);
            metadataResponse = await _httpClient.GetAsync(metadataUrl);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request error while fetching metadata for FileId: {FileId}", fileId);
            return null;
        }

        if (!metadataResponse.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Failed to get metadata from FileStoringService for FileId: {FileId}. Status: {StatusCode}", fileId,
                metadataResponse.StatusCode);
            return null;
        }

        var metadata = await metadataResponse.Content.ReadFromJsonAsync<FileMetadataFromStorageDto>();
        if (metadata == null)
        {
            _logger.LogWarning("Failed to deserialize metadata from FileStoringService for FileId: {FileId}", fileId);
            return null;
        }

        var downloadUrl = $"files/{fileId}/download";

        HttpResponseMessage fileResponse;
        try
        {
            _logger.LogDebug("Requesting file content from: {BaseAddress}{DownloadUrl}", _httpClient.BaseAddress,
                downloadUrl);
            fileResponse = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request error while downloading file content for FileId: {FileId}", fileId);
            return null;
        }


        if (!fileResponse.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Failed to download file from FileStoringService for FileId: {FileId}. Status: {StatusCode}", fileId,
                fileResponse.StatusCode);
            return null;
        }

        _logger.LogInformation("Successfully retrieved file data for FileId: {FileId}", fileId);
        return new FileData
        {
            Content = await fileResponse.Content.ReadAsStreamAsync(),
            ContentType = metadata.ContentType,
            OriginalFileName = metadata.OriginalFileName
        };
    }


    public FileStoringServiceClient(HttpClient httpClient, IConfiguration configuration,
        ILogger<FileStoringServiceClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _fileStoringServiceBaseUrl = configuration["ServiceUrls:FileStoringService"]
                                     ?? throw new InvalidOperationException(
                                         "FileStoringService URL is not configured in ServiceUrls:FileStoringService.");
        if (_httpClient.BaseAddress == null && !string.IsNullOrEmpty(_fileStoringServiceBaseUrl))
        {
            _httpClient.BaseAddress = new Uri(_fileStoringServiceBaseUrl.EndsWith('/')
                ? _fileStoringServiceBaseUrl
                : _fileStoringServiceBaseUrl + "/");
        }
        else if (_httpClient.BaseAddress == null)
        {
            _logger.LogError(
                "HttpClient BaseAddress is not set and FileStoringService URL is missing in configuration.");
        }
    }
}