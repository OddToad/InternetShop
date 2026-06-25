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

var builder = WebApplication.CreateBuilder(args);

// ==============================================================================
// 1. НАСТРОЙКА СТРУКТУРИРОВАННОГО ЛОГИРОВАНИЯ (Serilog)
// ==============================================================================
var loggerConfig = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext(); // Обязательно для автоматического подхвата TraceId (Correlation ID)

if (builder.Environment.IsDevelopment())
{
    // Понятный, цветной вывод для разработки локально в Visual Studio
    loggerConfig.WriteTo.Console(outputTemplate:
        "[{Timestamp:HH:mm:ss} {Level:u3}] [{TraceId}] {Message:lj}{NewLine}{Exception}");
}
else
{
    // Компактный JSON формат для контейнеров (Promtail -> Loki парсят его автоматически)
    loggerConfig.WriteTo.Console(new CompactJsonFormatter());
}

Log.Logger = loggerConfig.CreateLogger();
builder.Host.UseSerilog();


// ==============================================================================
// 3. ВНЕДРЕНИЕ ЗАВИСИМОСТЕЙ СЛОЕВ (Dependency Injection)
// ==============================================================================
// Подключаем слой бизнес-логики (наш метод расширения из TestService.Application)
builder.Services.AddApplication();

builder.Services.AddInfrastructure(builder.Configuration);

// Добавляем службы маршрутизации для Minimal APIs
builder.Services.AddEndpointsApiExplorer();


// ==============================================================================
// НАСТРОЙКА OPENAPI / SWAGGER С ПОДДЕРЖКОЙ KEYCLOAK (JWT)
// ==============================================================================
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        document.Info.Title = "TestService API (.NET 9)";
        document.Info.Version = "v1";
        document.Components ??= new Microsoft.OpenApi.Models.OpenApiComponents();

        // Жестко говорим Swagger, что все запросы к эндпоинтам должны идти через шлюз и префикс /api/test
        document.Servers.Clear();
        document.Servers.Add(new OpenApiServer { Url = "/api/test" });

        // Настройка безопасности (остается без изменений)
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
        options.MetadataAddress = builder.Configuration["Authentication:MetadataAddress"]!;
        options.Authority = builder.Configuration["Authentication:Authority"];
        options.Audience = builder.Configuration["Authentication:Audience"];
        options.RequireHttpsMetadata = false; // Отключаем HTTPS-валидацию для локальной Docker-сети
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuers = new[]
            {
                "http://localhost:5000/auth/realms/master",
                "http://keycloak:8080/realms/master" // на случай внутренних тестов
            },
            ValidateAudience = false, // Для базовой интеграции с Keycloak отключаем валидацию Audience
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(5) // Сглаживание разницы во времени между контейнерами
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

// 1. Генерируем json-спецификацию (внутри контейнера она живет по адресу /openapi/v1.json)
Microsoft.AspNetCore.Builder.OpenApiEndpointRouteBuilderExtensions.MapOpenApi(app);

// 2. Настройка интерфейса Swagger UI
Microsoft.AspNetCore.Builder.SwaggerUIBuilderExtensions.UseSwaggerUI(app, options =>
{
    // Говорим Swagger UI запрашивать схему по абсолютному пути через шлюз
    options.SwaggerEndpoint("/api/test/openapi/v1.json", "TestService API v1");
    options.RoutePrefix = "swagger";
});

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