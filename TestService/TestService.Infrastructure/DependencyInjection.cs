using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TestService.Application.Interfaces;
using TestService.Infrastructure;
using TestService.Infrastructure.Diagnostics;
using TestService.Infrastructure.Repositories;
using TestService.Infrastructure.Diagnostics;

namespace TestService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, 
        IConfiguration configuration)
    {
        // 1. Регистрируем DbContext
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(connectionString));

        // 2. Регистрируем сам репозиторий
        /services.AddScoped<IProductRepository, ProductRepository>();

        // РЕГИСТРАЦИЯ ДИАГНОСТИКИ
        services.AddSingleton<IConfigurationDiagnosticService, ConfigurationDiagnosticService>();

        return services;
    }
}