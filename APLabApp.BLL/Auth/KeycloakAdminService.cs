using Microsoft.Extensions.Configuration;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace APLabApp.BLL.Auth;

public class KeycloakAdminService : IKeycloakAdminService
{
    private readonly HttpClient _http;
    private readonly string _realm;
    private readonly string _baseUrl;
    private readonly string _clientId;
    private readonly string _clientSecret;

    public KeycloakAdminService(HttpClient http, IConfiguration cfg)
    {
        _http = http;
        _realm = cfg["Keycloak:Realm"] ?? "ApLabRealm";
        _baseUrl = (cfg["Keycloak:AuthServerUrl"] ?? "http://localhost:8080").TrimEnd('/');
        _clientId = cfg["Keycloak:ClientId"] ?? "aplab-api";
        _clientSecret = cfg["Keycloak:ClientSecret"] ?? "";
    }

    public async Task<bool> ResetPasswordAsync(Guid keycloakUserId, string newPassword, CancellationToken ct)
    {
        var token = await GetClientCredentialsToken(ct);
        if (token is null) return false;

        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var url = $"{_baseUrl}/admin/realms/{_realm}/users/{keycloakUserId}/reset-password";
        var payload = JsonSerializer.Serialize(new { type = "password", value = newPassword, temporary = false });
        var resp = await _http.PutAsync(url, new StringContent(payload, Encoding.UTF8, "application/json"), ct);
        return resp.IsSuccessStatusCode;
    }

    public async Task<bool> VerifyUserPasswordAsync(string usernameOrEmail, string password, CancellationToken ct)
    {
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["client_id"] = _clientId,
            ["client_secret"] = _clientSecret,
            ["username"] = usernameOrEmail,
            ["password"] = password
        };
        var resp = await _http.PostAsync($"{_baseUrl}/realms/{_realm}/protocol/openid-connect/token", new FormUrlEncodedContent(form), ct);
        return resp.IsSuccessStatusCode;
    }

    private async Task<string?> GetClientCredentialsToken(CancellationToken ct)
    {
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = _clientId,
            ["client_secret"] = _clientSecret
        };
        var resp = await _http.PostAsync($"{_baseUrl}/realms/{_realm}/protocol/openid-connect/token", new FormUrlEncodedContent(form), ct);
        if (!resp.IsSuccessStatusCode) return null;
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        return doc.RootElement.GetProperty("access_token").GetString();
    }
}
