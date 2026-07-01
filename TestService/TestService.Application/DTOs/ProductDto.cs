namespace TestService.Application.DTOs;

public record ProductDto(Guid Id, string Name, decimal Price, DateTime CreatedAt);
public record CreateProductCommand(string Name, decimal Price);
