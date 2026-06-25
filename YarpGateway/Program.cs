using Serilog;
using Serilog.Formatting.Compact;

var builder = WebApplication.CreateBuilder(args);

// Настройка Serilog в зависимости от среды окружения
var loggerConfig = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext();

if (builder.Environment.IsDevelopment())
{
    //вывод для локальной разработки в Visual Studio
    loggerConfig.WriteTo.Console(outputTemplate:
        "[{Timestamp:HH:mm:ss} {Level:u3}] [{TraceId}] {Message:lj}{NewLine}{Exception}");
}
else
{
    // Структурированный JSON для Promtail -> Loki при работе в Docker
    loggerConfig.WriteTo.Console(new CompactJsonFormatter());
}

Log.Logger = loggerConfig.CreateLogger();
builder.Host.UseSerilog();

// Добавляем поддержку YARP Reverse Proxy
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

// Маршрутизация запросов
app.UseRouting();

// MIDDLEWARE ДЛЯ ВОЗВРАТА CORRELATION ID КЛИЕНТУ
app.Use(async (context, next) =>
{
    // .NET 9 автоматически создает Activity для каждого входящего запроса
    var traceId = System.Diagnostics.Activity.Current?.TraceId.ToString()
                  ?? context.TraceIdentifier;

    // Добавляем TraceId в заголовки ответа, чтобы клиент его видел (например, "X-Correlation-Id")
    context.Response.Headers.Append("X-Correlation-Id", traceId);

    await next();
});

// Подключаем YARP
app.MapReverseProxy();

try
{
    Log.Information("Старт YARP API Gateway...");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "YARP API Gateway аварийно завершил работу");
}
finally
{
    Log.CloseAndFlush();
}