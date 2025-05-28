using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Readers;
using Microsoft.OpenApi.Writers;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// 1) Добавляем YARP-прокси
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// 2) HTTP-клиент для загрузки swagger.json с бэкендов
builder.Services.AddHttpClient("swagger_downloader")
    .AddStandardResilienceHandler(); // retry, timeout, circuit-breaker

// 3) Swagger на уровне Gateway
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(opts =>
{
    opts.SwaggerDoc("aggregated-v1", new OpenApiInfo
    {
        Title = "Aggregated API",
        Version = "v1",
        Description = "Все эндпоинты FileStoringService и FileAnalysisService"
    });
    // Отключаем автодетекцию контроллеров в самом Gateway
    opts.DocInclusionPredicate((docName, apiDesc) => false);
});

var app = builder.Build();

// 4) Ендпоинт-агрегатор swagger.json
app.MapGet("/swagger/v1/swagger.json", async (
        IConfiguration config,
        IHttpClientFactory httpFactory,
        ILoggerFactory logFactory) =>
    {
        var logger = logFactory.CreateLogger("SwaggerAggregator");
        var combined = new OpenApiDocument
        {
            Info = new OpenApiInfo { Title = "Aggregated API", Version = "v1" },
            Paths = new OpenApiPaths(),
            Servers = { new OpenApiServer { Url = "/" } },
            Components = new OpenApiComponents
            {
                Schemas = new Dictionary<string, OpenApiSchema>()
            }
        };

        var endpoints = config.GetSection("SwaggerEndpoints")
            .Get<List<SwaggerEndpointConfig>>()!;
        var client = httpFactory.CreateClient("swagger_downloader");
        var reader = new OpenApiStreamReader();

        foreach (var ep in endpoints)
        {
            try
            {
                var resp = await client.GetStreamAsync(ep.Url);
                var doc = reader.Read(resp, out var diag);
                // префикс схем, чтобы не было коллизий
                var prefix = ep.Key + "_";
                foreach (var (name, schema)
                         in doc.Components?.Schemas
                            ?? new Dictionary<string, OpenApiSchema>())
                    combined.Components.Schemas[prefix + name] = schema;

                // пути: заменяем ServicePathPrefixToReplace -> GatewayPathPrefix
                foreach (var (path, item) in doc.Paths)
                {
                    var newPath = path.StartsWith(ep.ServicePathPrefixToReplace)
                        ? ep.GatewayPathPrefix + path[ep.ServicePathPrefixToReplace.Length..]
                        : ep.GatewayPathPrefix + path;
                    combined.Paths[newPath] = item;
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Can't fetch Swagger from {Url}", ep.Url);
            }
        }

        // Серриализация в JSON
        await using var ms = new MemoryStream();

        using var sw = new StreamWriter(ms, new UTF8Encoding(false));

        // Создаём писатель
        var writer = new OpenApiJsonWriter(sw);

        // Записываем наш документ
        combined.SerializeAsV3(writer);
        writer.Flush();

        // Обязательно сбрасываем буфер StreamWriter
        sw.Flush();

        // Сбрасываем позицию MemoryStream и возвращаем его как результат
        ms.Position = 0;
        return Results.Stream(ms, "application/json");
    })
    .WithName("AggregateSwagger");

// 5) Собственно Reverse Proxy
app.MapReverseProxy();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Aggregated API v1");
    c.RoutePrefix = "swagger";
});

app.Run();

public class SwaggerEndpointConfig
{
    public string Key { get; set; } = "";
    public string Name { get; set; } = "";
    public string Url { get; set; } = "";
    public string GatewayPathPrefix { get; set; } = "";
    public string ServicePathPrefixToReplace { get; set; } = "";
}