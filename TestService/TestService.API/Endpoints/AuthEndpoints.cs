using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace TestService.API.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/auth");

        group.MapPost("/login", async (
            [FromBody] LoginRequest request,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory) =>
        {
            // 1. Валидация
            if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            {
                return Results.BadRequest(new { error = "Логин и пароль обязательны." });
            }

            // 2. Настройки Keycloak
            var authority = configuration["Authentication:Authority"];
            if (string.IsNullOrEmpty(authority))
            {
                return Results.Problem("Настройка 'Authentication:Authority' не найдена.");
            }

            var tokenEndpoint = $"{authority.TrimEnd('/')}/protocol/openid-connect/token";

            // 3. Параметры запроса (как в PowerShell)
            var tokenRequestParams = new List<KeyValuePair<string, string>>
            {
                new("grant_type", "password"),
                new("username", request.Username),
                new("password", request.Password),
                new("scope", "openid profile email")  // ← ДОБАВЛЯЕМ SCOPE!
            };

            var client = httpClientFactory.CreateClient();

            // 4. Basic Authentication
            var clientId = configuration["Authentication:ClientId"] ?? "shop-client";
            var clientSecret = configuration["Authentication:ClientSecret"] ?? "L9X1BdkMOzWmDXdgVOig1EQdRGkZpnT4";

            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);

            try
            {
                // 5. Отправка запроса
                var response = await client.PostAsync(
                    tokenEndpoint,
                    new FormUrlEncodedContent(tokenRequestParams));

                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    // Логируем ошибку для отладки
                    Console.WriteLine($"Keycloak error: {responseContent}");

                    return Results.Json(new
                    {
                        error = "Ошибка авторизации в Keycloak",
                        status = (int)response.StatusCode,
                        details = responseContent
                    }, statusCode: (int)response.StatusCode);
                }

                // 6. Успешный ответ
                var tokenData = await response.Content.ReadFromJsonAsync<KeycloakTokenResponse>();
                return Results.Ok(tokenData);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Не удалось связаться с Keycloak: {ex.Message}");
            }
        })
        .WithName("Login")
        .WithOpenApi(operation =>
        {
            operation.Summary = "Получение JWT токена по логину и паролю";
            operation.Description = "Direct Grant Flow через Keycloak";
            return operation;
        })
        .Produces<KeycloakTokenResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status500InternalServerError);
    }
}

public record LoginRequest(string Username, string Password);

public class KeycloakTokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    [JsonPropertyName("refresh_expires_in")]
    public int RefreshExpiresIn { get; set; }  // ← ДОБАВЛЯЕМ

    [JsonPropertyName("refresh_token")]
    public string RefreshToken { get; set; } = string.Empty;

    [JsonPropertyName("token_type")]
    public string TokenType { get; set; } = string.Empty;

    [JsonPropertyName("id_token")]
    public string IdToken { get; set; } = string.Empty;  // ← ДОБАВЛЯЕМ

    [JsonPropertyName("not-before-policy")]
    public int NotBeforePolicy { get; set; }  // ← ДОБАВЛЯЕМ

    [JsonPropertyName("session_state")]
    public string SessionState { get; set; } = string.Empty;  // ← ДОБАВЛЯЕМ

    [JsonPropertyName("scope")]
    public string Scope { get; set; } = string.Empty;
}