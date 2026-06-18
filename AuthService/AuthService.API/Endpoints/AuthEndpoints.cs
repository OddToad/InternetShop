using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;

namespace AuthService.API.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth");

        group.MapPost("/register", async (
            [FromBody] RegisterRequest dto,
            UserManager<IdentityUser> userManager,
            ILoggerFactory loggerFactory) => // 1. Внедряем фабрику вместо дженерика
        {
            // 2. Создаем логгер, передавая имя класса строкой
            var logger = loggerFactory.CreateLogger("AuthEndpoints");

            logger.LogInformation("Начало процесса регистрации для пользователя {UserEmail}", dto.Email);

            var user = new IdentityUser { UserName = dto.Email, Email = dto.Email };
            var result = await userManager.CreateAsync(user, dto.Password);

            if (!result.Succeeded)
            {
                logger.LogWarning("Не удалось создать пользователя {UserEmail}. Ошибки: {IdentityErrors}",
                    dto.Email, result.Errors.Select(e => e.Description));

                return Results.BadRequest(result.Errors);
            }

            var roleResult = await userManager.AddToRoleAsync(user, "User");

            if (!roleResult.Succeeded)
            {
                logger.LogError("Пользователь {UserEmail} создан, но не удалось назначить роль 'User'. Ошибки: {RoleErrors}",
                    dto.Email, roleResult.Errors.Select(e => e.Description));

                return Results.BadRequest(roleResult.Errors);
            }

            logger.LogInformation("Пользователь {UserEmail} успешно зарегистрирован с правами 'User'. ID пользователя: {UserId}",
                user.Email, user.Id);

            return Results.Ok(new { Message = "Пользователь успешно зарегистрирован с правами 'User'" });
        });
    }
}