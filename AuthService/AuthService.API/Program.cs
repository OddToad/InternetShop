using AuthService.Infrastructure.Data;
using AuthService.API.Endpoints; 
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using AuthService.API.Middleware;
using Serilog;
using Serilog.Formatting.Compact;

var builder = WebApplication.CreateBuilder(args);

// Настройка SERILOG
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration) // Позволяет настраивать уровни через appsettings.json
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "AuthService") // Метка микросервиса
    .Enrich.WithProperty("Environment", builder.Environment.EnvironmentName)
    // В режиме Development выводим структурированный JSON в консоль докера
    .WriteTo.Console(new RenderedCompactJsonFormatter())
    .CreateLogger();

builder.Host.UseSerilog();

// Настройка Базы Данных (PostgreSQL)
var connectionString = builder.Configuration.GetConnectionString("PostgresConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

// Настройка Identity API и РОЛЕЙ
builder.Services.AddIdentityApiEndpoints<IdentityUser>(options =>
{
    options.User.RequireUniqueEmail = true;
})
.AddRoles<IdentityRole>()
.AddEntityFrameworkStores<ApplicationDbContext>();

// Настройка Политик Авторизации
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireAdminRole", policy => policy.RequireRole("Admin"));
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Включаем Serilog логгер для HTTP-запросов (логирует маршруты, статус-коды, время выполнения)
app.UseSerilogRequestLogging();

// Подключаем CORRELATION ID MIDDLEWARE
app.UseMiddleware<CorrelationIdMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// МАРШРУТИЗАЦИЯ

// Встроенные системные эндпоинты Microsoft (/login, /refresh)
app.MapIdentityApi<IdentityUser>();

// эндпоинты
app.MapAuthEndpoints();
app.MapRoleEndpoints();
app.MapUserEndpoints();

// инициализация базы данных
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        // Применяем миграции
        await DatabaseMigrator.MigrateAsync(app.Services, logger, throwOnError: app.Environment.IsDevelopment());

        // Заполняем начальными данными
        await DbInitializer.SeedDataAsync(app.Services);

        logger.LogInformation("База данных успешно инициализирована");
    }
    catch (Exception ex)
    {
        logger.LogCritical(ex, "Критическая ошибка при инициализации базы данных");
        if (app.Environment.IsDevelopment())
        {
            throw;
        }
        return;
    }
}
app.Run();