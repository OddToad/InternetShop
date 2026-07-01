using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Formatting.Compact;
using TestService.Application;
using TestService.Infrastructure;
using TestService.API.Endpoints;
using Microsoft.OpenApi.Models;
using TestService.Infrastructure.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

//var loggerConfig = new LoggerConfiguration()
//    .ReadFrom.Configuration(builder.Configuration)
//    .Enrich.FromLogContext(); // Обязательно для автоматического подхвата TraceId (Correlation ID)

//if (builder.Environment.IsDevelopment())
//{
//    // Понятный, цветной вывод для разработки локально в Visual Studio
//    loggerConfig.WriteTo.Console(outputTemplate:
//        "[{Timestamp:HH:mm:ss} {Level:u3}] [{TraceId}] {Message:lj}{NewLine}{Exception}");
//}
//else
//{
//    // Компактный JSON формат для контейнеров (Promtail -> Loki парсят его автоматически)
//    loggerConfig.WriteTo.Console(new CompactJsonFormatter());
//}

//Log.Logger = loggerConfig.CreateLogger();

// ==============================================================================
// 1. НАСТРОЙКА СТРУКТУРИРОВАННОГО ЛОГИРОВАНИЯ (Serilog)
// ==============================================================================


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


// ==============================================================================
// 3. ВНЕДРЕНИЕ ЗАВИСИМОСТЕЙ СЛОЕВ (Dependency Injection)
// ==============================================================================
// Подключаем слой бизнес-логики (наш метод расширения из TestService.Application)
builder.Services.AddApplication();

builder.Services.AddInfrastructure(builder.Configuration);

// Добавляем службы маршрутизации для Minimal APIs
builder.Services.AddEndpointsApiExplorer();


builder.Services.AddHttpClient();

// ==============================================================================
// НАСТРОЙКА OPENAPI / SWAGGER С ПОДДЕРЖКОЙ KEYCLOAK (JWT)
// ==============================================================================
builder.Services.AddOpenApi("v1", options =>
{
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        document.Info.Title = "TestService API (.NET 9)";
        document.Info.Version = "v1";
        document.Components ??= new Microsoft.OpenApi.Models.OpenApiComponents();

        // Жестко говорим Swagger, что все запросы к эндпоинтам должны идти через шлюз и префикс /api/test
        document.Servers.Clear();
        document.Servers.Add(new OpenApiServer { Url = "/api/test" });

        // Настройка безопасности (оставляем без изменений)
        var securityScheme = new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Description = "Введите JWT токен в формате: Bearer {твой_токен}",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.Http,
            Scheme = "Bearer",
            BearerFormat = "JWT",
            Reference = new OpenApiReference
            {
                Id = "Bearer",
                Type = ReferenceType.SecurityScheme
            }
        };
        document.Components.SecuritySchemes.Add("Bearer", securityScheme);
        document.SecurityRequirements.Add(new OpenApiSecurityRequirement { { securityScheme, Array.Empty<string>() } });

        return Task.CompletedTask;
    });
});


// ==============================================================================
// 4. НАСТРОЙКА JWT АУТЕНТИФИКАЦИИ ОТ KEYCLOAK
// ==============================================================================
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var authority = builder.Configuration["Authentication:Authority"];
        options.Authority = authority;
        options.MetadataAddress = $"{authority.TrimEnd('/')}/.well-known/openid-configuration";
        options.Audience = builder.Configuration["Authentication:Audience"] ?? "account";
        options.RequireHttpsMetadata = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuers = new[]
            {
                authority,  // http://localhost:8080/realms/InternetShop
                "http://keycloak:8080/realms/InternetShop",  // Для внутренних Docker-запросов
                "http://localhost:5000/auth/realms/InternetShop"  // Если есть прокси
            },
            ValidateAudience = false,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(5)
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

//ЗАПУСК ДИАГНОСТИКИ
using (var scope = app.Services.CreateScope())
{
    var diagnosticService = scope.ServiceProvider.GetRequiredService<IConfigurationDiagnosticService>();
    diagnosticService.LogConfiguration();
}

// ==============================================================================
// 5. АВТОМАТИЧЕСКАЯ ИНИЦИАЛИЗАЦИЯ И МИГРАЦИЯ БАЗЫ ДАННЫХ
// ==============================================================================
using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Log.Information("Применение миграций к базе данных PostgreSQL...");
        db.Database.Migrate();
        Log.Information("Миграции успешно применены.");
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Ошибка при автоматическом применении миграций базы данных.");
    }
}


app.UseCors();
// ==============================================================================
// 6. MIDDLEWARE И МАРШРУТИЗАЦИЯ (Minimal APIs)
// ==============================================================================
app.UseAuthentication();
app.UseAuthorization();

// 1. Генерируем json-спецификацию (явно привязываем роут к документу v1)
app.MapOpenApi("/openapi/{documentName}.json");

// 2. Настройка интерфейса Swagger UI
app.UseSwaggerUI(options =>
{
    // Говорим Swagger UI запрашивать схему по абсолютному пути через шлюз
    options.SwaggerEndpoint("/api/test/openapi/v1.json", "TestService API v1");
    options.RoutePrefix = "swagger";
});

app.MapAuthEndpoints();
app.MapProductEndpoints();

// ==============================================================================
// 7. ЗАПУСК ПРИЛОЖЕНИЯ
// ==============================================================================
try
{
    Log.Information("Старт микросервиса TestService.API на платформе .NET 9...");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Микросервис TestService.API аварийно завершил работу.");
}
finally
{
    Log.CloseAndFlush();
}