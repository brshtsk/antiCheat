namespace FileAnalysisService.Clients.WordCloud;

public class ImageSpecs
{
    public int Width { get; set; } = 1920;
    public int Height { get; set; } = 1080;
    public string BackgroundColor { get; set; } = "black";
    public string FontFamily { get; set; } = "Arial";
    public int FontSize { get; set; } = 20;
    public string Language { get; set; } = "ru";
    public string Format { get; set; } = "png";
    public string FontColor { get; set; } = "white";
    public double FontScale { get; set; } = 1.0;
    public bool RemoveStopwords { get; set; } = true;
    public bool UseWordList { get; set; } = false;
    public string Scale { get; set; } = "linear";
}

public interface IWordCloudClient
{
    Task<Stream?> GenerateWordCloudAsync(string text, ImageSpecs? parameters = null);
}