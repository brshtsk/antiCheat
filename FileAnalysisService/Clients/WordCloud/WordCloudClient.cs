using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace FileAnalysisService.Clients.WordCloud
{
    public class WordCloudClient : IWordCloudClient
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<WordCloudClient> _logger;
        private readonly JsonSerializerOptions _jsonOptions;

        public WordCloudClient(HttpClient httpClient, ILogger<WordCloudClient> logger)
        {
            _httpClient = httpClient;
            _logger = logger;

            if (_httpClient.BaseAddress == null)
            {
                _logger.LogError("HttpClient BaseAddress is not configured for WordCloudClient.");
                throw new InvalidOperationException("HttpClient BaseAddress is not configured.");
            }

            _jsonOptions = new JsonSerializerOptions
            {
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            };
        }

        public async Task<Stream?> GenerateWordCloudAsync(string text, ImageSpecs? parameters = null)
        {
            _logger.LogInformation(
                "Generating word cloud for text start: '{Preview}...'",
                text.Length <= 50 ? text : text.Substring(0, 50));

            var requestBody = new
            {
                text = text,
                width = parameters?.Width,
                height = parameters?.Height,
                backgroundColor = parameters?.BackgroundColor,
                fontFamily = parameters?.FontFamily,
                fontSize = parameters?.FontSize,
                fontColor = parameters?.FontColor,
                fontScale = parameters?.FontScale,
                removeStopwords = parameters?.RemoveStopwords,
                language = parameters?.Language,
                format = parameters?.Format,
                useWordList = parameters?.UseWordList
            };

            try
            {
                _logger.LogDebug(
                    "POST {Url} with body preview: {{ format = {Format}, width = {Width}, height = {Height} }}",
                    _httpClient.BaseAddress, requestBody.format, requestBody.width, requestBody.height);

                HttpResponseMessage response = await _httpClient
                    .PostAsJsonAsync("wordcloud/generate", requestBody, _jsonOptions);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation(
                        "Word cloud generated: Content-Type = {ContentType}",
                        response.Content.Headers.ContentType);
                    return await response.Content.ReadAsStreamAsync();
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    _logger.LogError(
                        "Error generating word cloud: {Status} / {Error}",
                        response.StatusCode, error);
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception while calling WordCloud API");
                return null;
            }
        }
    }
}