using Microsoft.Extensions.Configuration;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;

namespace APLabApp.BLL.Auth
{
    public class KeycloakAdminService : IKeycloakAdminService
    {
        private readonly HttpClient _http;
        private readonly string _realm;
        private readonly string _baseUrl;
        private readonly string _adminClientId;
        private readonly string _adminClientSecret;
        private readonly string _publicClientId;
        private readonly string _publicClientSecret;

        public KeycloakAdminService(HttpClient http, IConfiguration cfg)
        {
            _http = http;
            _realm = cfg["Keycloak:Realm"] ?? "ApLabRealm";
            _baseUrl = (cfg["Keycloak:AuthServerUrl"] ?? "http://localhost:8080").TrimEnd('/');
            _adminClientId = cfg["Keycloak:AdminClientId"] ?? cfg["Keycloak:ClientId"] ?? "aplab-admin";
            _adminClientSecret = cfg["Keycloak:AdminClientSecret"] ?? cfg["Keycloak:ClientSecret"] ?? "";
            _publicClientId = cfg["Keycloak:PublicClientId"] ?? cfg["Keycloak:ClientId"] ?? "aplab-api";
            _publicClientSecret = cfg["Keycloak:PublicClientSecret"] ?? cfg["Keycloak:ClientSecret"] ?? "";
        }

        public async Task<Guid?> CreateUserAsync(string username, string email, string fullName, string password, string role, CancellationToken ct, bool temporaryPassword = false)
        {
            await EnsureAdminAuth(ct);
            var usersUrl = $"{_baseUrl}/admin/realms/{_realm}/users";
            var (firstName, lastName) = SplitFullName(fullName);
            var payload = new { username, email, firstName, lastName, enabled = true, emailVerified = true };
            var resp = await _http.PostAsync(usersUrl, JsonContent(payload), ct);
            var createBody = await resp.Content.ReadAsStringAsync(ct);
            string? idStr = null;
            if (resp.IsSuccessStatusCode)
            {
                var location = resp.Headers.Location?.ToString();
                if (!string.IsNullOrWhiteSpace(location)) idStr = location.Split('/').LastOrDefault();
            }
            else if ((int)resp.StatusCode == 409)
            {
                var found = await FindUserIdByUsernameOrEmail(username, email, ct);
                if (found.HasValue) idStr = found.Value.ToString();
            }
            else
            {
                throw new InvalidOperationException($"[KC] Create user failed: {(int)resp.StatusCode} {createBody}");
            }
            if (string.IsNullOrWhiteSpace(idStr) || !Guid.TryParse(idStr, out var keycloakId))
                throw new InvalidOperationException("[KC] Could not resolve created user id.");
            var pwdPayload = new { type = "password", value = password, temporary = temporaryPassword };
            var pwdResp = await _http.PutAsync($"{usersUrl}/{keycloakId}/reset-password", JsonContent(pwdPayload), ct);
            if (!pwdResp.IsSuccessStatusCode)
            {
                var b = await pwdResp.Content.ReadAsStringAsync(ct);
                throw new InvalidOperationException($"[KC] Set password failed: {(int)pwdResp.StatusCode} {b}");
            }
            if (!string.IsNullOrWhiteSpace(role))
            {
                var roleName = role.Trim().ToLowerInvariant();
                if (roleName != "guest")
                {
                    var guestGroupId = await FindGroupIdByName("guest", ct);
                    if (guestGroupId is not null)
                        await _http.DeleteAsync($"{usersUrl}/{keycloakId}/groups/{guestGroupId}", ct);
                }
                var groupId = await FindGroupIdByName(roleName, ct);
                if (groupId is not null)
                {
                    var join = await _http.PutAsync($"{usersUrl}/{keycloakId}/groups/{groupId}", new StringContent(string.Empty), ct);
                    if (!join.IsSuccessStatusCode)
                    {
                        var b = await join.Content.ReadAsStringAsync(ct);
                        throw new InvalidOperationException($"[KC] Join group '{roleName}' failed: {(int)join.StatusCode} {b}");
                    }
                }
                else
                {
                    await AssignRealmRoleIfExists(usersUrl, keycloakId, roleName, ct);
                }
            }
            return keycloakId;
        }

        public async Task<bool> ResetPasswordAsync(Guid keycloakUserId, string newPassword, CancellationToken ct)
        {
            await EnsureAdminAuth(ct);
            var url = $"{_baseUrl}/admin/realms/{_realm}/users/{keycloakUserId}/reset-password";
            var payload = new { type = "password", value = newPassword, temporary = false };
            var resp = await _http.PutAsync(url, JsonContent(payload), ct);
            return resp.IsSuccessStatusCode;
        }

        public async Task<bool> VerifyUserPasswordAsync(string usernameOrEmail, string password, CancellationToken ct)
        {
            var form = new Dictionary<string, string>
            {
                ["grant_type"] = "password",
                ["client_id"] = _publicClientId,
                ["username"] = usernameOrEmail,
                ["password"] = password
            };
            if (!string.IsNullOrWhiteSpace(_publicClientSecret))
                form["client_secret"] = _publicClientSecret;
            var resp = await _http.PostAsync($"{_baseUrl}/realms/{_realm}/protocol/openid-connect/token", new FormUrlEncodedContent(form), ct);
            return resp.IsSuccessStatusCode;
        }

        public async Task<TokenResponse> PasswordTokenAsync(string usernameOrEmail, string password, CancellationToken ct)
        {
            var form = new Dictionary<string, string>
            {
                ["grant_type"] = "password",
                ["client_id"] = _publicClientId,
                ["username"] = usernameOrEmail,
                ["password"] = password
            };
            if (!string.IsNullOrWhiteSpace(_publicClientSecret))
                form["client_secret"] = _publicClientSecret;
            var resp = await _http.PostAsync($"{_baseUrl}/realms/{_realm}/protocol/openid-connect/token", new FormUrlEncodedContent(form), ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
                throw new InvalidOperationException($"[KC] Login failed: {(int)resp.StatusCode} {body}");
            var token = JsonSerializer.Deserialize<TokenResponse>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
            return token;
        }

        public async Task<bool> IsUserInGroupAsync(Guid keycloakUserId, string groupName, CancellationToken ct)
        {
            await EnsureAdminAuth(ct);
            var url = $"{_baseUrl}/admin/realms/{_realm}/users/{keycloakUserId}/groups?briefRepresentation=true";
            var resp = await _http.GetAsync(url, ct);
            if (!resp.IsSuccessStatusCode) return false;
            var body = await resp.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(body);
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                if (el.TryGetProperty("name", out var n) && string.Equals(n.GetString(), groupName, StringComparison.OrdinalIgnoreCase)) return true;
                if (el.TryGetProperty("path", out var p) && p.GetString() is string path && path.EndsWith("/" + groupName, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        public async Task ReplaceGroupsWithAsync(Guid keycloakUserId, string groupName, CancellationToken ct)
        {
            await EnsureAdminAuth(ct);
            var groupsResp = await _http.GetAsync($"{_baseUrl}/admin/realms/{_realm}/users/{keycloakUserId}/groups?briefRepresentation=true", ct);
            if (!groupsResp.IsSuccessStatusCode) return;
            var groupsBody = await groupsResp.Content.ReadAsStringAsync(ct);
            using var groupsDoc = JsonDocument.Parse(groupsBody);
            var current = groupsDoc.RootElement.EnumerateArray().Select(x => new
            {
                Id = x.GetProperty("id").GetString(),
                Name = x.GetProperty("name").GetString()
            }).ToList();
            var targetId = await FindGroupIdByName(groupName, ct);
            if (string.IsNullOrWhiteSpace(targetId)) return;
            foreach (var g in current)
            {
                if (!string.Equals(g.Name, groupName, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(g.Id))
                    await _http.DeleteAsync($"{_baseUrl}/admin/realms/{_realm}/users/{keycloakUserId}/groups/{g.Id}", ct);
            }
            var hasTarget = current.Any(c => string.Equals(c.Name, groupName, StringComparison.OrdinalIgnoreCase));
            if (!hasTarget)
                await _http.PutAsync($"{_baseUrl}/admin/realms/{_realm}/users/{keycloakUserId}/groups/{targetId}", new StringContent(string.Empty), ct);
        }

        public async Task<bool> UpdateUserProfileAsync(Guid keycloakUserId, string? firstName, string? lastName, IDictionary<string, string?>? attributes, CancellationToken ct)
        {
            await EnsureAdminAuth(ct);
            var payload = new Dictionary<string, object?>();
            if (firstName != null) payload["firstName"] = firstName;
            if (lastName != null) payload["lastName"] = lastName;
            if (attributes is not null)
            {
                var map = new Dictionary<string, string[]?>();
                foreach (var kv in attributes) map[kv.Key] = kv.Value is null ? null : new[] { kv.Value };
                payload["attributes"] = map;
            }
            var url = $"{_baseUrl}/admin/realms/{_realm}/users/{keycloakUserId}";
            var resp = await _http.PutAsync(url, JsonContent(payload), ct);
            return resp.IsSuccessStatusCode;
        }

        public async Task<HashSet<Guid>> GetUserIdsInRealmRoleAsync(string role, CancellationToken ct)
        {
            await EnsureAdminAuth(ct);
            var result = new HashSet<Guid>();
            var safeRole = Uri.EscapeDataString(role);
            var first = 0;
            const int max = 100;
            while (true)
            {
                var url = $"{_baseUrl}/admin/realms/{_realm}/roles/{safeRole}/users?first={first}&max={max}";
                var resp = await _http.GetAsync(url, ct);
                if (!resp.IsSuccessStatusCode) break;
                var body = await resp.Content.ReadAsStringAsync(ct);
                using var doc = JsonDocument.Parse(body);
                var arr = doc.RootElement.ValueKind == JsonValueKind.Array ? doc.RootElement.EnumerateArray().ToList() : new List<JsonElement>();
                if (arr.Count == 0) break;
                foreach (var el in arr)
                {
                    var idStr = el.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
                    if (Guid.TryParse(idStr, out var id)) result.Add(id);
                }
                if (arr.Count < max) break;
                first += max;
            }
            return result;
        }

        public async Task<HashSet<Guid>> GetUserIdsInGroupAsync(string groupName, CancellationToken ct)
        {
            await EnsureAdminAuth(ct);
            var result = new HashSet<Guid>();
            var groupId = await FindGroupIdByName(groupName, ct);
            if (string.IsNullOrWhiteSpace(groupId)) return result;
            var first = 0;
            const int max = 100;
            while (true)
            {
                var url = $"{_baseUrl}/admin/realms/{_realm}/groups/{groupId}/members?first={first}&max={max}";
                var resp = await _http.GetAsync(url, ct);
                if (!resp.IsSuccessStatusCode) break;
                var body = await resp.Content.ReadAsStringAsync(ct);
                using var doc = JsonDocument.Parse(body);
                var arr = doc.RootElement.ValueKind == JsonValueKind.Array ? doc.RootElement.EnumerateArray().ToList() : new List<JsonElement>();
                if (arr.Count == 0) break;
                foreach (var el in arr)
                {
                    var idStr = el.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
                    if (Guid.TryParse(idStr, out var id)) result.Add(id);
                }
                if (arr.Count < max) break;
                first += max;
            }
            return result;
        }

        public async Task<Dictionary<Guid, string[]>> GetRealmRolesBulkAsync(IEnumerable<Guid> keycloakUserIds, CancellationToken ct)
        {
            await EnsureAdminAuth(ct);
            var ids = keycloakUserIds.Distinct().ToArray();
            var map = new ConcurrentDictionary<Guid, string[]>();
            var gate = new SemaphoreSlim(8);
            var tasks = ids.Select(async id =>
            {
                await gate.WaitAsync(ct);
                try
                {
                    var url = $"{_baseUrl}/admin/realms/{_realm}/users/{id}/role-mappings/realm";
                    var resp = await _http.GetAsync(url, ct);
                    if (!resp.IsSuccessStatusCode)
                    {
                        map.TryAdd(id, Array.Empty<string>());
                        return;
                    }
                    var body = await resp.Content.ReadAsStringAsync(ct);
                    using var doc = JsonDocument.Parse(body);
                    var roles = doc.RootElement.ValueKind == JsonValueKind.Array
                        ? doc.RootElement.EnumerateArray()
                            .Select(r => r.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "")
                            .Where(s => !string.IsNullOrWhiteSpace(s))
                            .ToArray()
                        : Array.Empty<string>();
                    map.TryAdd(id, roles);
                }
                finally
                {
                    gate.Release();
                }
            });
            await Task.WhenAll(tasks);
            return map.ToDictionary(kv => kv.Key, kv => kv.Value);
        }

        public async Task<Dictionary<Guid, string[]>> GetGroupsBulkAsync(IEnumerable<Guid> keycloakUserIds, CancellationToken ct)
        {
            await EnsureAdminAuth(ct);
            var ids = keycloakUserIds.Distinct().ToArray();
            var map = new ConcurrentDictionary<Guid, string[]>();
            var gate = new SemaphoreSlim(8);
            var tasks = ids.Select(async id =>
            {
                await gate.WaitAsync(ct);
                try
                {
                    var url = $"{_baseUrl}/admin/realms/{_realm}/users/{id}/groups?briefRepresentation=true";
                    var resp = await _http.GetAsync(url, ct);
                    if (!resp.IsSuccessStatusCode)
                    {
                        map.TryAdd(id, Array.Empty<string>());
                        return;
                    }
                    var body = await resp.Content.ReadAsStringAsync(ct);
                    using var doc = JsonDocument.Parse(body);
                    var groups = doc.RootElement.ValueKind == JsonValueKind.Array
                        ? doc.RootElement.EnumerateArray()
                            .Select(g => g.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "")
                            .Where(s => !string.IsNullOrWhiteSpace(s))
                            .ToArray()
                        : Array.Empty<string>();
                    map.TryAdd(id, groups);
                }
                finally
                {
                    gate.Release();
                }
            });
            await Task.WhenAll(tasks);
            return map.ToDictionary(kv => kv.Key, kv => kv.Value);
        }

        private async Task EnsureAdminAuth(CancellationToken ct)
        {
            var token = await GetAdminToken(ct);
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        private async Task<string> GetAdminToken(CancellationToken ct)
        {
            var form = new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = _adminClientId,
                ["client_secret"] = _adminClientSecret
            };
            var resp = await _http.PostAsync($"{_baseUrl}/realms/{_realm}/protocol/openid-connect/token", new FormUrlEncodedContent(form), ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
                throw new InvalidOperationException($"[KC] Admin token failed: {(int)resp.StatusCode} {body}");
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.GetProperty("access_token").GetString()!;
        }

        private static StringContent JsonContent(object o) =>
            new StringContent(JsonSerializer.Serialize(o), Encoding.UTF8, "application/json");

        private static (string first, string last) SplitFullName(string fullName)
        {
            var parts = (fullName ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
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

        private async Task<string?> FindGroupIdByName(string groupName, CancellationToken ct)
        {
            var resp = await _http.GetAsync($"{_baseUrl}/admin/realms/{_realm}/groups?search={Uri.EscapeDataString(groupName)}", ct);
            if (!resp.IsSuccessStatusCode) return null;
            var body = await resp.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(body);
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                if (el.TryGetProperty("name", out var n) && string.Equals(n.GetString(), groupName, StringComparison.OrdinalIgnoreCase))
                    return el.GetProperty("id").GetString();
            }
            return null;
        }

        private async Task AssignRealmRoleIfExists(string usersUrl, Guid keycloakUserId, string roleName, CancellationToken ct)
        {
            var roleResp = await _http.GetAsync($"{_baseUrl}/admin/realms/{_realm}/roles/{roleName}", ct);
            if (!roleResp.IsSuccessStatusCode) return;
            var roleBody = await roleResp.Content.ReadAsStringAsync(ct);
            using var roleDoc = JsonDocument.Parse(roleBody);
            var roleObj = new
            {
                id = roleDoc.RootElement.GetProperty("id").GetString(),
                name = roleDoc.RootElement.GetProperty("name").GetString()
            };
            var roleJson = JsonSerializer.Serialize(new[] { roleObj });
            await _http.PostAsync($"{usersUrl}/{keycloakUserId}/role-mappings/realm", new StringContent(roleJson, Encoding.UTF8, "application/json"), ct);
        }
    }

    public sealed class TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = "";
        [JsonPropertyName("refresh_token")]
        public string RefreshToken { get; set; } = "";
        [JsonPropertyName("token_type")]
        public string TokenType { get; set; } = "";
        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
        [JsonPropertyName("refresh_expires_in")]
        public int RefreshExpiresIn { get; set; }
        [JsonPropertyName("scope")]
        public string Scope { get; set; } = "";
    }
}