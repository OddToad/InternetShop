using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using System.Text.Json.Serialization;

namespace TestService.API.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        // Делаем группу /auth
        var group = app.MapGroup("/auth");

        group.MapPost("/login", async (
            [FromBody] LoginRequest request,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory) =>
        {
            if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            {
                return Results.BadRequest(new { error = "Логин и пароль обязательны." });
            }

            var authority = configuration["Authentication:Authority"];
            var audience = configuration["Authentication:Audience"];

            if (string.IsNullOrEmpty(authority) || string.IsNullOrEmpty(audience))
            {
                return Results.Problem("Настройки Keycloak не найдены.");
            }

            var tokenEndpoint = $"{authority.TrimEnd('/')}/protocol/openid-connect/token";
            var client = httpClientFactory.CreateClient();

            var tokenRequestParams = new Dictionary<string, string>
            {
                { "grant_type", "password" },
                { "client_id", audience },
                { "username", request.Username },
                { "password", request.Password }
            };

            try
            {
                var response = await client.PostAsync(tokenEndpoint, new FormUrlEncodedContent(tokenRequestParams));

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    return Results.Json(new { error = "Ошибка Keycloak", details = errorContent }, statusCode: (int)response.StatusCode);
                }

                var tokenData = await response.Content.ReadFromJsonAsync<KeycloakTokenResponse>();
                return Results.Ok(tokenData);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Ошибка сети: {ex.Message}");
            }
        })
        .WithName("Login")
        .WithOpenApi()
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

    [JsonPropertyName("refresh_token")]
    public string RefreshToken { get; set; } = string.Empty;

    [JsonPropertyName("token_type")]
    public string TokenType { get; set; } = string.Empty;
}