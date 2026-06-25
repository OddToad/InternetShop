using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TestService.Application.Interfaces;
using TestService.Infrastructure;
using TestService.Infrastructure.Repositories;

namespace TestService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // 1. Регистрируем DbContext (переносим сюда из Program.cs)
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(connectionString));

        // 2. Регистрируем сам репозиторий. Здесь Инфраструктура видит И интерфейс, И класс!
        services.AddScoped<IProductRepository, ProductRepository>();

        return services;
    }
}