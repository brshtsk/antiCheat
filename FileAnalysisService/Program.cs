using System;
using FileAnalysisService.Clients.WordCloud;
using FileAnalysisService.Data;
using FileAnalysisService.Services;
using FileAnalysisService.Services.FileStorage;
using FileAnalysisService.Services.Orchestrator;
using FileAnalysisService.Services.PlagiatDetector;
using FileAnalysisService.Services.StatisticsCounter;
using FileAnalysisService.Services.WordCloud;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Подключаем DbContext с PostgreSQL
var connectionString = builder.Configuration.GetConnectionString("AppDatabase");
_ = connectionString ?? throw new InvalidOperationException(
    "Connection string 'AppDatabase' not found.");
builder.Services.AddDbContext<FileAnalysisDbContext>(options =>
    options.UseNpgsql(connectionString));

// Настройки локального хранения файлов для анализа
builder.Services.Configure<FileStorageSettings>(
    builder.Configuration.GetSection("FileStorageForAnalysis"));
builder.Services.AddScoped<IFileStorageProvider, LocalFileStorageProvider>();

// Клиент для генерации облака слов (если используется внешнее API)
builder.Services.AddHttpClient<IWordCloudClient, WordCloudClient>(client =>
{
    var baseUrl = builder.Configuration["WordCloudApi:BaseUrl"];
    if (string.IsNullOrEmpty(baseUrl))
        throw new InvalidOperationException("WordCloudApi:BaseUrl не настроен.");
    if (!baseUrl.EndsWith("/")) baseUrl += "/";
    client.BaseAddress = new Uri(baseUrl);
});

// Регистрация сервисов анализа
builder.Services.AddScoped<IStatisticsCounter, StatisticsCounter>();
builder.Services.AddScoped<IPlagiatDetector, PlagiatDetector>();
builder.Services.AddScoped<IWordCloudService, WordCloudService>();

// Оркестратор: собирает вместе все этапы анализа
builder.Services.AddScoped<IFileAnalysisOrchestrator, FileAnalysisOrchestrator>();

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
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "FileAnalysisService API V1"));

    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<FileAnalysisDbContext>();
        try
        {
            db.Database.Migrate();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("Применены миграции к FileAnalysisDB.");
        }
        catch (Exception ex)
        {
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
            logger.LogError(ex, "Ошибка при миграции FileAnalysisDB.");
        }
    }
}

// app.UseHttpsRedirection(); // включить при необходимости
app.UseAuthorization();
app.MapControllers();
app.Run();