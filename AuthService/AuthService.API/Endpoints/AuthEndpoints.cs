using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace AuthService.API.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth");

        // 1. Регистрация нового пользователя с правами 'User' по умолчанию
        group.MapPost("/register", async (
            [FromBody] RegisterRequest dto,
            UserManager<IdentityUser> userManager) =>
        {
            var user = new IdentityUser { UserName = dto.Email, Email = dto.Email };
            var result = await userManager.CreateAsync(user, dto.Password);

            if (!result.Succeeded) return Results.BadRequest(result.Errors);

            var roleResult = await userManager.AddToRoleAsync(user, "User");

            if (!roleResult.Succeeded)
            {
                return Results.BadRequest(roleResult.Errors);
            }

            return Results.Ok(new { Message = "Пользователь успешно зарегистрирован с правами 'User'" });
        });
    }
}