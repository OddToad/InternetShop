using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

var builder = WebApplication.CreateBuilder(args);
// НАСТРОЙКА ЛОГИРОВАНИЯ SERILOG
var loggerConfiguration = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", builder.Environment.ApplicationName)
    .Enrich.WithProperty("Environment", builder.Environment.EnvironmentName);

var consoleFormat = builder.Configuration["LoggingSettings:ConsoleFormat"];

if (string.Equals(consoleFormat, "Json", StringComparison.OrdinalIgnoreCase))
{
    loggerConfiguration.WriteTo.Console(new RenderedCompactJsonFormatter());
}
else
{
    loggerConfiguration.WriteTo.Console(
        outputTemplate:
        "[{Timestamp:HH:mm:ss.fff} {Level:u3}] {Message:lj}{NewLine}{Exception}");
}

Log.Logger = loggerConfiguration.CreateLogger();

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