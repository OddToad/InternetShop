using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

var builder = WebApplication.CreateBuilder(args);

// Проверка Docker
var inDocker = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true";

// Настройка логирования
var loggerConfig = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
    .MinimumLevel.Override("Yarp", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "YARP-Gateway")
    .Enrich.WithProperty("Environment", builder.Environment.EnvironmentName);

if (inDocker)
{
    // В Docker - JSON для Loki
    loggerConfig.WriteTo.Console(new RenderedCompactJsonFormatter());
}
else
{
    // Локально - читаемый текст
    loggerConfig.WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss.fff} {Level:u3}] {Message:lj}{NewLine}{Exception}"
    );
}

Log.Logger = loggerConfig.CreateLogger();
builder.Host.UseSerilog();

// Добавляем YARP
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

// Middleware для чистого логирования
app.Use(async (context, next) =>
{
    var traceId = System.Diagnostics.Activity.Current?.TraceId.ToString()
                  ?? context.TraceIdentifier;

    Log.Information("→ {Method} {Path}",
        context.Request.Method,
        context.Request.Path);

    await next();

    Log.Information("← {StatusCode} {Method} {Path}",
        context.Response.StatusCode,
        context.Request.Method,
        context.Request.Path);
});

app.UseRouting();
app.MapReverseProxy();

try
{
    Log.Information("YARP Gateway started in {Environment}",
        builder.Environment.EnvironmentName);
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "YARP Gateway crashed");
}
finally
{
    Log.CloseAndFlush();
}