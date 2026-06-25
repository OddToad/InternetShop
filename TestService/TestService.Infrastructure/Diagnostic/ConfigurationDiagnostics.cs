using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace TestService.Infrastructure.Diagnostics;

public class ConfigurationDiagnosticService : IConfigurationDiagnosticService
{
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<ConfigurationDiagnosticService> _logger;

    public ConfigurationDiagnosticService(
        IConfiguration configuration,
        IHostEnvironment environment,
        ILogger<ConfigurationDiagnosticService> logger)
    {
        _configuration = configuration;
        _environment = environment;
        _logger = logger;
    }

    public void LogConfiguration()
    {
        _logger.LogInformation(new string('=', 60));
        _logger.LogInformation("ДИАГНОСТИКА КОНФИГУРАЦИИ");
        _logger.LogInformation(new string('=', 60));

        _logger.LogInformation("1. Информация о среде:");
        _logger.LogInformation("   Environment: {Environment}", _environment.EnvironmentName);
        _logger.LogInformation("   ContentRootPath: {ContentRootPath}", _environment.ContentRootPath);
        _logger.LogInformation("   ApplicationName: {ApplicationName}", _environment.ApplicationName);

        // 2. Поиск appsettings файлов
        _logger.LogInformation("\n2. Поиск appsettings файлов:");
        try
        {
            var files = Directory.GetFiles(_environment.ContentRootPath, "appsettings*.json");
            foreach (var file in files)
            {
                var fileName = Path.GetFileName(file);
                try
                {
                    var content = File.ReadAllText(file);
                    var hasInternetShop = content.Contains("InternetShop");
                    var hasMaster = content.Contains("master");
                    var hasLocalhost = content.Contains("localhost");

                    _logger.LogInformation("   - {FileName}", fileName);
                    _logger.LogInformation("     • InternetShop: {HasInternetShop}", hasInternetShop);
                    _logger.LogInformation("     • master: {HasMaster}", hasMaster);
                    _logger.LogInformation("     • localhost: {HasLocalhost}", hasLocalhost);

                    // Показываем Authority из файла
                    if (content.Contains("Authority"))
                    {
                        var lines = content.Split('\n');
                        foreach (var line in lines)
                        {
                            if (line.Contains("Authority") && line.Contains(":"))
                            {
                                _logger.LogInformation("     • Authority в файле: {Line}", line.Trim());
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("   - {FileName} (ошибка чтения: {Error})", fileName, ex.Message);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Ошибка при поиске файлов: {Error}", ex.Message);
        }

        // 3. Загруженные провайдеры конфигурации
        _logger.LogInformation("\n3. Загруженные провайдеры конфигурации:");
        if (_configuration is IConfigurationRoot root)
        {
            foreach (var provider in root.Providers)
            {
                _logger.LogInformation("   - {ProviderType}", provider.GetType().Name);

                if (provider is Microsoft.Extensions.Configuration.Json.JsonConfigurationProvider jsonProvider)
                {
                    try
                    {
                        var field = jsonProvider.GetType().GetField("_source",
                            BindingFlags.NonPublic | BindingFlags.Instance);
                        if (field != null)
                        {
                            var source = field.GetValue(jsonProvider);
                            var pathProp = source?.GetType().GetProperty("Path");
                            if (pathProp != null)
                            {
                                var path = pathProp.GetValue(source)?.ToString();
                                _logger.LogInformation("     Файл: {FilePath}", path ?? "unknown");
                            }
                        }
                    }
                    catch
                    {
                        // Игнорируем ошибки рефлексии
                    }
                }
            }
        }

        // 4. Ключевые настройки
        _logger.LogInformation("\n4. Ключевые настройки:");
        var authority = _configuration["Authentication:Authority"];
        var metadataAddress = _configuration["Authentication:MetadataAddress"];
        var clientId = _configuration["Authentication:ClientId"];
        var clientSecret = _configuration["Authentication:ClientSecret"];
        var connectionString = _configuration.GetConnectionString("DefaultConnection");

        _logger.LogInformation("   Authentication:Authority = {Authority}", authority ?? "NULL");
        _logger.LogInformation("   Authentication:MetadataAddress = {MetadataAddress}", metadataAddress ?? "NULL");
        _logger.LogInformation("   Authentication:ClientId = {ClientId}", clientId ?? "NULL");
        _logger.LogInformation("   Authentication:ClientSecret = {ClientSecret}",
            string.IsNullOrEmpty(clientSecret) ? "NULL" : clientSecret.Substring(0, Math.Min(10, clientSecret.Length)) + "...");
        _logger.LogInformation("   ConnectionStrings:DefaultConnection = {ConnectionString}",
            connectionString ?? "NULL");

        // 5. Проверка на ошибки
        if (!IsAuthorityCorrect())
        {
            _logger.LogWarning("\n⚠️ ВНИМАНИЕ! Authority указывает на master или отсутствует!");
            _logger.LogWarning("   Текущее значение: {Authority}", authority ?? "NULL");
            _logger.LogWarning("   Ожидаемое значение: http://localhost:8080/realms/InternetShop");
        }
        else
        {
            _logger.LogInformation("\n✅ Authority настроен правильно: {Authority}", authority);
        }

        // 6. Проверка наличия всех необходимых настроек
        var missingSettings = new List<string>();
        if (string.IsNullOrEmpty(authority)) missingSettings.Add("Authentication:Authority");
        if (string.IsNullOrEmpty(clientId)) missingSettings.Add("Authentication:ClientId");
        if (string.IsNullOrEmpty(clientSecret)) missingSettings.Add("Authentication:ClientSecret");
        if (string.IsNullOrEmpty(connectionString)) missingSettings.Add("ConnectionStrings:DefaultConnection");

        if (missingSettings.Any())
        {
            _logger.LogWarning("\n⚠️ Отсутствуют настройки: {MissingSettings}", string.Join(", ", missingSettings));
        }

        _logger.LogInformation(new string('=', 60));
        _logger.LogInformation("");
    }

    public bool IsAuthorityCorrect()
    {
        var authority = _configuration["Authentication:Authority"];
        return !string.IsNullOrEmpty(authority) &&
               !authority.Contains("master") &&
               authority.Contains("InternetShop");
    }
}