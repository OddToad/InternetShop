using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using TestService.Application.DTOs;
using TestService.Application.Interfaces;
using TestService.Application.Services;

namespace TestService.API.Endpoints;

public static class ProductEndpoints
{
    public static void MapProductEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/");

        // 1. Получение (вызываем сервис)
        group.MapGet("products", async (IProductService productService) =>
        {
            var products = await productService.GetAllProductsAsync();
            return Results.Ok(products);
        });

        // 2. Создание (вызываем сервис)
        group.MapPost("products", async (
            [FromBody] CreateProductCommand command,
            ClaimsPrincipal userPrincipal,
            IProductService productService) =>
        {
            var userId = userPrincipal.FindFirst("sub")?.Value;

            if (string.IsNullOrWhiteSpace(command.Name) || command.Price <= 0)
            {
                return Results.BadRequest("Некорректные данные.");
            }

            var result = await productService.CreateProductAsync(command);
            return Results.Created($"/api/test/products/{result.Id}", result);
        })
        .RequireAuthorization();
    }
}