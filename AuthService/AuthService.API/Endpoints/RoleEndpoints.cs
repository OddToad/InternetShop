using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace AuthService.API.Endpoints;

public static class RoleEndpoints
{
    public static void MapRoleEndpoints(this IEndpointRouteBuilder app)
    {
        // Создаем группу эндпоинтов и СРАЗУ защищаем её политикой админа
        var group = app.MapGroup("/api/roles")
                       .RequireAuthorization("RequireAdminRole");

        // 1. Создание новой роли в системе (например, для других микросервисов)
        group.MapPost("/create", async (
            [FromQuery] string roleName,
            RoleManager<IdentityRole> roleManager) =>
        {
            if (string.IsNullOrWhiteSpace(roleName)) return Results.BadRequest("Имя роли не может быть пустым");

            var roleExists = await roleManager.RoleExistsAsync(roleName);
            if (roleExists) return Results.BadRequest("Такая роль уже существует");

            var result = await roleManager.CreateAsync(new IdentityRole(roleName));
            if (!result.Succeeded) return Results.BadRequest(result.Errors);

            return Results.Ok(new { Message = $"Роль '{roleName}' успешно создана в системе" });
        });

        // 2. Назначение роли пользователю по Email
        group.MapPost("/assign", async (
            [FromQuery] string userEmail,
            [FromQuery] string roleName,
            UserManager<IdentityUser> userManager,
            RoleManager<IdentityRole> roleManager) =>
        {
            var user = await userManager.FindByEmailAsync(userEmail);
            if (user == null) return Results.NotFound("Пользователь не найден");

            var roleExists = await roleManager.RoleExistsAsync(roleName);
            if (!roleExists) return Results.NotFound($"Роль '{roleName}' не зарегистрирована в системе");

            if (await userManager.IsInRoleAsync(user, roleName))
            {
                return Results.BadRequest("Пользователь уже обладает этой ролью");
            }

            var result = await userManager.AddToRoleAsync(user, roleName);
            if (!result.Succeeded) return Results.BadRequest(result.Errors);

            return Results.Ok(new { Message = $"Пользователю {userEmail} успешно присвоена роль '{roleName}'" });
        });
    }
}