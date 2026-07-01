using Microsoft.Extensions.Logging;
using TestService.Application.DTOs;
using TestService.Application.Interfaces;
using TestService.Domain.Entities;

namespace TestService.Application.Services;

public class ProductService : IProductService
{
    private readonly IProductRepository _repository;
    private readonly ILogger<ProductService> _logger;

    public ProductService(IProductRepository repository, ILogger<ProductService> _logger)
    {
        _repository = repository;
        this._logger = _logger;
    }

    public async Task<IEnumerable<ProductDto>> GetAllProductsAsync()
    {
        _logger.LogInformation("Запрос на получение списка всех продуктов из базы данных.");

        var products = await _repository.GetAllAsync();

        var result = products.Select(p => new ProductDto(p.Id, p.Name, p.Price, p.CreatedAt)).ToList();

        _logger.LogInformation("Успешно получено {Count} продуктов.", result.Count);

        return result;
    }

    public async Task<ProductDto> CreateProductAsync(CreateProductCommand command)
    {
        _logger.LogInformation("Начало процесса создания нового продукта. Имя: {ProductName}, Цена: {ProductPrice}",
            command.Name, command.Price);

        try
        {
            var product = new Product
            {
                Id = Guid.NewGuid(),
                Name = command.Name,
                Price = command.Price,
                CreatedAt = DateTime.UtcNow
            };

            await _repository.AddAsync(product);
            await _repository.SaveChangesAsync();

            _logger.LogInformation("Продукт {ProductName} успешно создан с ID: {ProductId}",
                product.Name, product.Id);

            return new ProductDto(product.Id, product.Name, product.Price, product.CreatedAt);
        }
        catch (Exception ex)
        {
            // Передаем объект исключения ex первым аргументом. 
            // Serilog автоматически распарсит StackTrace и запишет его в поле "Exception" в Loki
            _logger.LogError(ex, "Ошибка при добавлении продукта {ProductName} в базу данных.", command.Name);
            throw;
        }
    }
}