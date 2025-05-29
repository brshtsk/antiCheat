using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Readers;
using Microsoft.OpenApi.Writers;
using System.Text;
using System.Net;

var builder = WebApplication.CreateBuilder(args);

// Добавляем YARP-прокси
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// HTTP-клиент для загрузки swagger.json с бэкендов
builder.Services.AddHttpClient("swagger_downloader", client =>
{
    // допустим, ждём не более 5 секунд:
    client.Timeout = TimeSpan.FromSeconds(5);
});


// Swagger на уровне Gateway
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

app.Logger.LogInformation("(ﾉ◕ヮ◕)ﾉ*:･ﾟ✧ API Gateway starting!");

// Перехватываем ошибки, если сервисы выключены
app.Use(async (HttpContext context, Func<Task> next) =>
{
    try
    {
        await next();

        // YARP может сам выставить 502 Bad Gateway
        if (context.Response.StatusCode == (int)HttpStatusCode.BadGateway)
        {
            await ReturnServiceDown(context);
        }
    }
    catch (HttpRequestException)
    {
        // Сетевые ошибки считаем падением downstream
        await ReturnServiceDown(context);
    }
});

// Swagger
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/custom/v1/swagger.json", "Aggregated API v1");
    c.RoutePrefix = "swagger";
});

// Ендпоинт-агрегатор swagger.json
app.MapGet("/swagger/custom/v1/swagger.json", async (
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
        
        combined.Servers.Clear();

        var endpoints = config.GetSection("SwaggerEndpoints")
            .Get<List<SwaggerEndpointConfig>>()!;
        var client = httpFactory.CreateClient("swagger_downloader");
        var reader = new OpenApiStreamReader();

        foreach (var ep in endpoints)
        {
            try
            {
                // 1) Делаем простой GET с таймаутом HttpClient
                var response = await client.GetAsync(ep.Url);
                if (!response.IsSuccessStatusCode)
                {
                    logger.LogWarning("Сервис {Url} вернул {StatusCode}, пропускаем", ep.Url, response.StatusCode);
                    continue;
                }

                // 2) Читаем и парсим
                await using var stream = await response.Content.ReadAsStreamAsync();
                var doc = reader.Read(stream, out var diag);
                if (diag.Errors.Any())
                {
                    logger.LogWarning("Парсинг {Url} дал ошибки: {Errors}",
                        ep.Url, string.Join("; ", diag.Errors.Select(e => e.Message)));
                    continue;
                }
                
                // 3) Добавляем серверы
                foreach (var server in doc.Servers)
                    combined.Servers.Add(new OpenApiServer {
                        Url = ep.GatewayPathPrefix == "/"
                            ? server.Url
                            : server.Url.TrimEnd('/') + ep.GatewayPathPrefix
                    });

                // 4) Мёржим схемы
                foreach (var (name, schema) in doc.Components?.Schemas
                                               ?? new Dictionary<string, OpenApiSchema>())
                {
                    combined.Components.Schemas[name] = schema;
                }

                // 5) Мёржим пути
                foreach (var (path, item) in doc.Paths)
                {
                    var newPath = path.StartsWith(ep.ServicePathPrefixToReplace, StringComparison.OrdinalIgnoreCase)
                        ? ep.GatewayPathPrefix + path[ep.ServicePathPrefixToReplace.Length..]
                        : ep.GatewayPathPrefix + path;
                    combined.Paths[newPath] = item;
                }
            }
            catch (Exception ex)
            {
                // Ловим любой TimeoutException, HttpRequestException, парсинг и т.д.
                logger.LogWarning(ex, "Не удалось получить или спарсить Swagger от {Url}", ep.Url);
                // ПЕРЕХОДИМ К СЛЕДУЮЩЕМУ endpoint’у
                continue;
            }
        }

        // Всегда отдаем документ — пусть даже пустой
        await using var ms = new MemoryStream();
        using var sw = new StreamWriter(ms, new UTF8Encoding(false));
        var writer = new OpenApiJsonWriter(sw);
        combined.SerializeAsV3(writer);
        sw.Flush();
        ms.Position = 0;

        string json = await new StreamReader(ms).ReadToEndAsync();
        return Results.Content(json, "application/json; charset=utf-8");
    })
    .WithName("GetAggregatedSwaggerJson")
    .Produces<string>();


// Reverse Proxy
app.MapReverseProxy();

app.Run();

// Возвращаем 503 + JSON, если сервис недоступен
static Task ReturnServiceDown(HttpContext context)
{
    // Определяем, к какому сервису шёл запрос
    var path = context.Request.Path.Value ?? "";
    string svcName = path.StartsWith("/files", StringComparison.OrdinalIgnoreCase)
        ? "FileStoringService"
        : path.StartsWith("/analysis", StringComparison.OrdinalIgnoreCase)
            ? "FileAnalysisService"
            : "UnknownService";

    // Чистим ответ и возвращаем 503 + JSON
    context.Response.Clear();
    context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
    context.Response.ContentType = "application/json";
    return context.Response.WriteAsJsonAsync(new
    {
        error = $"{svcName} is currently unavailable."
    });
}

public class SwaggerEndpointConfig
{
    public string Key { get; set; } = "";
    public string Name { get; set; } = "";
    public string Url { get; set; } = "";
    public string GatewayPathPrefix { get; set; } = "";
    public string ServicePathPrefixToReplace { get; set; } = "";
}