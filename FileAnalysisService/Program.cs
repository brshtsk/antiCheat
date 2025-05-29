using System;
using FileAnalysisService.Clients.FileStoring;
using FileAnalysisService.Clients.WordCloud;
using FileAnalysisService.Data;
using FileAnalysisService.Services;
using FileAnalysisService.Services.FileStorage;
using FileAnalysisService.Services.Orchestrator;
using FileAnalysisService.Services.PlagiatDetector;
using FileAnalysisService.Services.StatisticsCounter;
using FileAnalysisService.Services.WordCloud;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

var builder = WebApplication.CreateBuilder(args);

// 1. Подключаем DbContext с PostgreSQL
var connectionString = builder.Configuration.GetConnectionString("AppDatabase")
    ?? throw new InvalidOperationException("Connection string 'AppDatabase' not found.");
builder.Services.AddDbContext<FileAnalysisDbContext>(options =>
    options.UseNpgsql(connectionString));

// 2. Настройки локального хранения файлов для анализа
builder.Services.Configure<FileStorageSettings>(
    builder.Configuration.GetSection("FileStorageForAnalysis"));
builder.Services.AddScoped<IFileStorageProvider, LocalFileStorageProvider>();

// 3. HTTP-клиент для FileStoringService
builder.Services.AddHttpClient<IFileStoringServiceClient, FileStoringServiceClient>(client =>
{
    var fileStoringUrl = builder.Configuration["ServiceUrls:FileStoringService"];
    if (string.IsNullOrEmpty(fileStoringUrl))
        throw new InvalidOperationException("ServiceUrls:FileStoringService is not configured.");
    if (!fileStoringUrl.EndsWith("/"))
        fileStoringUrl += "/";
    client.BaseAddress = new Uri(fileStoringUrl);
});

// 4. HTTP-клиент для генерации облака слов (если используется внешний API)
builder.Services.AddHttpClient<IWordCloudClient, WordCloudClient>(client =>
{
    var baseUrl = builder.Configuration["WordCloudApi:BaseUrl"];
    if (string.IsNullOrEmpty(baseUrl))
        throw new InvalidOperationException("WordCloudApi:BaseUrl is not configured.");
    if (!baseUrl.EndsWith("/"))
        baseUrl += "/";
    client.BaseAddress = new Uri(baseUrl);
});

// 5. Регистрация сервисов анализа
builder.Services.AddScoped<IStatisticsCounter, StatisticsCounter>();
builder.Services.AddScoped<IPlagiatDetector, PlagiatDetector>();
builder.Services.AddScoped<IWordCloudService, WordCloudService>();

// 6. Оркестратор: собирает вместе все этапы анализа
builder.Services.AddScoped<IFileAnalysisOrchestrator, FileAnalysisOrchestrator>();

// 7. Контроллеры, Swagger, OpenAPI
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "FileAnalysisService API",
        Version = "v1"
    });
});

var app = builder.Build();

// Миграции при старте в Development
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "FileAnalysisService API v1"));

    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<FileAnalysisDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        db.Database.Migrate();
        logger.LogInformation("Applied migrations to FileAnalysisDB successfully.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred while migrating FileAnalysisDB.");
    }
}

// app.UseHttpsRedirection(); // ToDo: юзать, если нужен HTTPS
app.UseAuthorization();
app.MapControllers();
app.Run();
