using Microsoft.Extensions.DependencyInjection;
using TestService.Application.Interfaces;
using TestService.Application.Services;

namespace TestService.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Регистрируем бизнес-сервис
        services.AddScoped<IProductService, ProductService>();

        return services;
    }
}