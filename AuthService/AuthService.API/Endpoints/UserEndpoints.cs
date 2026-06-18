using System.Security.Claims;
using AuthService.Domain.Entities;
using AuthService.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthService.API.Endpoints;

// Входящие данные от фронтенда
public record UpdateProfileDto(string FirstName, string? LastName, string? PhoneNumber);

public static class UserEndpoints
{
    public static void MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/users")
                       .RequireAuthorization(); // Доступно только авторизованным пользователям

        // Эндпоинт создания/обновления профиля пользователя
        group.MapPost("/profile", async (
            [FromBody] UpdateProfileDto dto,
            ClaimsPrincipal userPrincipal,
            ApplicationDbContext db) =>
        {
            // Достаем ID пользователя из его JWT токена
            var userId = userPrincipal.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Results.Unauthorized();
            }

            if (string.IsNullOrWhiteSpace(dto.FirstName))
            {
                return Results.BadRequest("Имя не может быть пустым");
            }

            // Ищем, есть ли уже профиль в базе для этого пользователя
            var profile = await db.UserProfiles.FirstOrDefaultAsync(p => p.IdentityUserId == userId);

            if (profile == null)
            {
                // Если профиля нет — создаем новую запись
                profile = new UserProfile
                {
                    Id = Guid.NewGuid(),
                    IdentityUserId = userId,
                    FirstName = dto.FirstName,
                    LastName = dto.LastName,
                    PhoneNumber = dto.PhoneNumber
                };
                await db.UserProfiles.AddAsync(profile);
            }
            else
            {
                // Профиль найден
                profile.FirstName = dto.FirstName;
                profile.LastName = dto.LastName;
                profile.PhoneNumber = dto.PhoneNumber;
               
            }

            // 5. Сохраняем изменения в PostgreSQL
            await db.SaveChangesAsync();

            return Results.Ok(new { Message = "Информация о пользователе успешно обновлена" });
        });

        //тестовый метод /me
        group.MapGet("/me", (ClaimsPrincipal user) =>
        {
            return Results.Ok(new
            {
                Id = user.FindFirstValue(ClaimTypes.NameIdentifier),
                Email = user.FindFirstValue(ClaimTypes.Email),
                Roles = user.FindAll(ClaimTypes.Role).Select(c => c.Value)
            });
        });
    }
}