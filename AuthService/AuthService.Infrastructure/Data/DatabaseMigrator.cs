using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AuthService.Infrastructure.Data;

public static class DatabaseMigrator
{
    public static async Task MigrateAsync(IServiceProvider serviceProvider, ILogger logger, bool throwOnError = false)
    {
        try
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var pendingMigrations = await context.Database.GetPendingMigrationsAsync();
            var pendingMigrationsList = pendingMigrations.ToList();

            if (pendingMigrationsList.Any())
            {
                logger.LogInformation("Применение {Count} миграций: {Migrations}",
                    pendingMigrationsList.Count,
                    string.Join(", ", pendingMigrationsList));

                await context.Database.MigrateAsync();

                logger.LogInformation("Миграции успешно применены");
            }
            else
            {
                logger.LogInformation("База данных уже актуальна, миграции не требуются");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при применении миграций");
            if (throwOnError)
                throw;
        }
    }
}