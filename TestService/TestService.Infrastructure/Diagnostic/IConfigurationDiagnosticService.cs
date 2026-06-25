namespace TestService.Infrastructure.Diagnostics;

public interface IConfigurationDiagnosticService
{
    /// <summary>
    /// Выводит диагностическую информацию о конфигурации
    /// </summary>
    void LogConfiguration();

    /// <summary>
    /// Проверяет, правильный ли Authority указан в конфигурации
    /// </summary>
    bool IsAuthorityCorrect();
}