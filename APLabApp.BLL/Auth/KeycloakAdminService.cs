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

    public async Task<Guid?> CreateUserAsync(string username, string email, string fullName, string password, string role, CancellationToken ct)
    {
        var token = await GetClientCredentialsToken(ct);
        if (token is null) return null;

        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var usersUrl = $"{_baseUrl}/admin/realms/{_realm}/users";
        var (firstName, lastName) = SplitFullName(fullName);

        var payload = new
        {
            username,
            email,
            firstName,
            lastName,
            enabled = true,
            emailVerified = true
        };

        var resp = await _http.PostAsync(usersUrl, new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"), ct);

        string? idStr = null;

        if (resp.IsSuccessStatusCode)
        {
            var location = resp.Headers.Location?.ToString();
            if (!string.IsNullOrWhiteSpace(location))
                idStr = location.Split('/').LastOrDefault();
        }
        else if ((int)resp.StatusCode == 409)
        {
            var found = await FindUserIdByUsernameOrEmail(username, email, ct);
            if (found.HasValue) idStr = found.Value.ToString();
        }
        else
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(idStr) || !Guid.TryParse(idStr, out var keycloakId)) return null;

        var pwdPayload = new { type = "password", value = password, temporary = false };
        await _http.PutAsync($"{usersUrl}/{keycloakId}/reset-password", new StringContent(JsonSerializer.Serialize(pwdPayload), Encoding.UTF8, "application/json"), ct);

        if (!string.IsNullOrWhiteSpace(role))
        {
            var roleResp = await _http.GetAsync($"{_baseUrl}/admin/realms/{_realm}/roles/{role}", ct);
            if (roleResp.IsSuccessStatusCode)
            {
                var roleBody = await roleResp.Content.ReadAsStringAsync(ct);
                using var roleDoc = JsonDocument.Parse(roleBody);
                var roleName = roleDoc.RootElement.GetProperty("name").GetString();

                var assignedResp = await _http.GetAsync($"{usersUrl}/{keycloakId}/role-mappings/realm", ct);
                var alreadyAssigned = false;
                if (assignedResp.IsSuccessStatusCode)
                {
                    var arrText = await assignedResp.Content.ReadAsStringAsync(ct);
                    using var arrDoc = JsonDocument.Parse(arrText);
                    foreach (var el in arrDoc.RootElement.EnumerateArray())
                    {
                        if (el.TryGetProperty("name", out var n) && n.GetString() == roleName)
                        {
                            alreadyAssigned = true;
                            break;
                        }
                    }
                }

                if (!alreadyAssigned)
                {
                    var arr = new[] { roleDoc.RootElement };
                    var roleJson = JsonSerializer.Serialize(arr);
                    await _http.PostAsync($"{usersUrl}/{keycloakId}/role-mappings/realm", new StringContent(roleJson, Encoding.UTF8, "application/json"), ct);
                }
            }
        }

        return keycloakId;
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

    private static (string, string) SplitFullName(string fullName)
    {
        var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length switch
        {
            >= 2 => (parts[0], string.Join(' ', parts.Skip(1))),
            1 => (parts[0], ""),
            _ => ("", "")
        };
    }

    private async Task<Guid?> FindUserIdByUsernameOrEmail(string username, string email, CancellationToken ct)
    {
        var r1 = await _http.GetAsync($"{_baseUrl}/admin/realms/{_realm}/users?username={Uri.EscapeDataString(username)}&exact=true", ct);
        if (r1.IsSuccessStatusCode)
        {
            var body = await r1.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0)
            {
                var idStr = doc.RootElement[0].GetProperty("id").GetString();
                if (Guid.TryParse(idStr, out var id)) return id;
            }
        }

        var r2 = await _http.GetAsync($"{_baseUrl}/admin/realms/{_realm}/users?email={Uri.EscapeDataString(email)}", ct);
        if (r2.IsSuccessStatusCode)
        {
            var body = await r2.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0)
            {
                var idStr = doc.RootElement[0].GetProperty("id").GetString();
                if (Guid.TryParse(idStr, out var id)) return id;
            }
        }

        return null;
    }
}
