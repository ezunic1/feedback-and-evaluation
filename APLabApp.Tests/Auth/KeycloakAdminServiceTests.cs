using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using APLabApp.BLL.Auth;
using APLabApp.BLL.Errors;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace APLabApp.Tests.Auth
{
    public class KeycloakAdminServiceTests
    {
        private static IConfiguration CreateDefaultConfig()
        {
            var dict = new Dictionary<string, string?>
            {
                ["Keycloak:Realm"] = "TestRealm",
                ["Keycloak:AuthServerUrl"] = "http://kc.local",
                ["Keycloak:AdminClientId"] = "admin-client",
                ["Keycloak:AdminClientSecret"] = "admin-secret",
                ["Keycloak:PublicClientId"] = "public-client",
                ["Keycloak:PublicClientSecret"] = "public-secret"
            };

            return new ConfigurationBuilder()
                .AddInMemoryCollection(dict!)
                .Build();
        }

        private static HttpClient CreateHttpClient(params Func<HttpRequestMessage, HttpResponseMessage>[] responses)
        {
            var handler = new QueueMessageHandler(responses);
            return new HttpClient(handler)
            {
                BaseAddress = new Uri("http://kc.local")
            };
        }

        [Fact]
        public async Task ResetPasswordAsync_ReturnsTrueOnSuccess()
        {
            var http = CreateHttpClient(
                _ => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"access_token\":\"admintoken\"}", Encoding.UTF8, "application/json")
                },
                _ => new HttpResponseMessage(HttpStatusCode.NoContent)
            );

            var svc = new KeycloakAdminService(http, CreateDefaultConfig());

            var ok = await svc.ResetPasswordAsync(Guid.NewGuid(), "NewPassword1!", CancellationToken.None);

            Assert.True(ok);
        }

        [Fact]
        public async Task ResetPasswordAsync_AdminTokenFailure_Throws()
        {
            var http = CreateHttpClient(
                _ => new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Content = new StringContent("fail", Encoding.UTF8, "application/json")
                }
            );

            var svc = new KeycloakAdminService(http, CreateDefaultConfig());

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                svc.ResetPasswordAsync(Guid.NewGuid(), "pass", CancellationToken.None));
        }

        [Fact]
        public async Task VerifyUserPasswordAsync_ReturnsTrueAndFalse()
        {
            var http = CreateHttpClient(
                _ => new HttpResponseMessage(HttpStatusCode.OK),
                _ => new HttpResponseMessage(HttpStatusCode.BadRequest)
            );

            var svc = new KeycloakAdminService(http, CreateDefaultConfig());

            var ok = await svc.VerifyUserPasswordAsync("user", "pass", CancellationToken.None);
            var fail = await svc.VerifyUserPasswordAsync("user", "bad", CancellationToken.None);

            Assert.True(ok);
            Assert.False(fail);
        }

        [Fact]
        public async Task PasswordTokenAsync_Success_ReturnsTokenResponse()
        {
            var tokenJson = """
            {
              "access_token": "a",
              "refresh_token": "r",
              "token_type": "Bearer",
              "expires_in": 3600,
              "refresh_expires_in": 7200,
              "scope": "openid"
            }
            """;

            var http = CreateHttpClient(
                _ => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(tokenJson, Encoding.UTF8, "application/json")
                }
            );

            var svc = new KeycloakAdminService(http, CreateDefaultConfig());

            var token = await svc.PasswordTokenAsync("user", "pass", CancellationToken.None);

            Assert.Equal("a", token.AccessToken);
            Assert.Equal("r", token.RefreshToken);
            Assert.Equal("Bearer", token.TokenType);
            Assert.Equal(3600, token.ExpiresIn);
            Assert.Equal(7200, token.RefreshExpiresIn);
        }

        [Fact]
        public async Task PasswordTokenAsync_ErrorAccountNotSetup_ThrowsPasswordChangeRequired()
        {
            var body = """
            {
              "error": "invalid_grant",
              "error_description": "Account is not fully set up"
            }
            """;

            var http = CreateHttpClient(
                _ => new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json")
                }
            );

            var svc = new KeycloakAdminService(http, CreateDefaultConfig());

            await Assert.ThrowsAsync<PasswordChangeRequiredException>(() =>
                svc.PasswordTokenAsync("user", "pass", CancellationToken.None));
        }

        [Fact]
        public async Task PasswordTokenAsync_ErrorWithoutRequiredAction_ThrowsInvalidOperation()
        {
            var http = CreateHttpClient(
                _ => new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Content = new StringContent("{\"error\":\"invalid_grant\",\"error_description\":\"bad creds\"}", Encoding.UTF8, "application/json")
                },
                _ => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"access_token\":\"admintoken\"}", Encoding.UTF8, "application/json")
                },
                _ => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("[]", Encoding.UTF8, "application/json")
                },
                _ => new HttpResponseMessage(HttpStatusCode.NotFound)
            );

            var svc = new KeycloakAdminService(http, CreateDefaultConfig());

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                svc.PasswordTokenAsync("user", "pass", CancellationToken.None));
        }

        [Fact]
        public void BuildBrowserAuthUrl_WithNullRedirect_ReturnsAccountUrl()
        {
            var http = CreateHttpClient();
            var svc = new KeycloakAdminService(http, CreateDefaultConfig());

            var url = svc.BuildBrowserAuthUrl(null, null);

            Assert.Contains("/account/#/security/signingin", url);
        }

        [Fact]
        public void BuildBrowserAuthUrl_WithRedirectAndState_BuildsAuthUrl()
        {
            var http = CreateHttpClient();
            var svc = new KeycloakAdminService(http, CreateDefaultConfig());

            var url = svc.BuildBrowserAuthUrl("https://app.local/callback", "xyz");

            Assert.Contains("/protocol/openid-connect/auth", url);
            Assert.Contains("redirect_uri=", url);
            Assert.Contains("state=xyz", url);
        }

        [Fact]
        public async Task IsUserInGroupAsync_FindsByNameOrPath()
        {
            var groupsJson = """
            [
              { "id":"1", "name":"other", "path":"/all/other" },
              { "id":"2", "name":"test-group", "path":"/all/test-group" }
            ]
            """;

            var http = CreateHttpClient(
                _ => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"access_token\":\"admintoken\"}", Encoding.UTF8, "application/json")
                },
                _ => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(groupsJson, Encoding.UTF8, "application/json")
                }
            );

            var svc = new KeycloakAdminService(http, CreateDefaultConfig());

            var ok = await svc.IsUserInGroupAsync(Guid.NewGuid(), "test-group", CancellationToken.None);
            Assert.True(ok);
        }

        [Fact]
        public async Task IsUserInGroupAsync_NonSuccess_ReturnsFalse()
        {
            var http = CreateHttpClient(
                _ => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"access_token\":\"admintoken\"}", Encoding.UTF8, "application/json")
                },
                _ => new HttpResponseMessage(HttpStatusCode.InternalServerError)
            );

            var svc = new KeycloakAdminService(http, CreateDefaultConfig());

            var ok = await svc.IsUserInGroupAsync(Guid.NewGuid(), "g", CancellationToken.None);
            Assert.False(ok);
        }

        [Fact]
        public async Task ReplaceGroupsWithAsync_RemovesOthersAndAddsTarget()
        {
            var currentGroupsJson = """
    [
      { "id":"1", "name":"old" }
    ]
    """;

            var groupsSearchJson = """
    [
      { "id":"2", "name":"keep" }
    ]
    """;

            var deleteCalled = new List<string>();
            var putCalled = false;

            var http = new HttpClient(new QueueMessageHandler(
                new Func<HttpRequestMessage, HttpResponseMessage>[]
                {
            _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"access_token\":\"admintoken\"}", Encoding.UTF8, "application/json")
            },
            _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(currentGroupsJson, Encoding.UTF8, "application/json")
            },
            _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(groupsSearchJson, Encoding.UTF8, "application/json")
            },
            req =>
            {
                deleteCalled.Add(req.RequestUri!.ToString());
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            },
            req =>
            {
                putCalled = true;
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }
                }))
            {
                BaseAddress = new Uri("http://kc.local")
            };

            var svc = new KeycloakAdminService(http, CreateDefaultConfig());

            await svc.ReplaceGroupsWithAsync(Guid.NewGuid(), "keep", CancellationToken.None);

            Assert.Single(deleteCalled);
            Assert.Contains("groups/1", deleteCalled[0]);
            Assert.True(putCalled);
        }


        [Fact]
        public async Task UpdateUserProfileAsync_BuildsPayloadAndReturnsTrue()
        {
            string? capturedBody = null;

            var http = CreateHttpClient(
                _ => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"access_token\":\"admintoken\"}", Encoding.UTF8, "application/json")
                },
                req =>
                {
                    capturedBody = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                    return new HttpResponseMessage(HttpStatusCode.NoContent);
                }
            );

            var svc = new KeycloakAdminService(http, CreateDefaultConfig());

            var attrs = new Dictionary<string, string?>
            {
                ["k1"] = "v1",
                ["k2"] = null
            };

            var ok = await svc.UpdateUserProfileAsync(Guid.NewGuid(), "First", "Last", attrs, CancellationToken.None);

            Assert.True(ok);
            Assert.NotNull(capturedBody);
            Assert.Contains("First", capturedBody);
            Assert.Contains("Last", capturedBody);
            Assert.Contains("k1", capturedBody);
            Assert.Contains("k2", capturedBody);
        }

        [Fact]
        public async Task GetUserIdsInRealmRoleAsync_ParsesIdsFromResponse()
        {
            var id1 = Guid.Parse("00000000-0000-0000-0000-000000000001");
            var id2 = Guid.Parse("00000000-0000-0000-0000-000000000002");

            var json = $@"
    [
        {{ ""id"": ""{id1}"" }},
        {{ ""id"": ""{id2}"" }}
    ]";

            var http = CreateHttpClient(
             
                _ => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"access_token\":\"admintoken\"}", Encoding.UTF8, "application/json")
                },
                _ => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                }
            );

            var svc = new KeycloakAdminService(http, CreateDefaultConfig());

            var ids = await svc.GetUserIdsInRealmRoleAsync("intern", CancellationToken.None);

            Assert.NotNull(ids);
            Assert.Contains(id1, ids);
      
        }


        [Fact]
        public async Task GetUserIdsInGroupAsync_ReturnsEmptyWhenGroupNotFound()
        {
            var http = CreateHttpClient(
                _ => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"access_token\":\"admintoken\"}", Encoding.UTF8, "application/json")
                },
                _ => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("[]", Encoding.UTF8, "application/json")
                }
            );

            var svc = new KeycloakAdminService(http, CreateDefaultConfig());

            var ids = await svc.GetUserIdsInGroupAsync("group", CancellationToken.None);

            Assert.Empty(ids);
        }

        [Fact]
        public async Task GetUserIdsInGroupAsync_PaginatesAndParses()
        {
            var groupsSearchJson = """
            [
              { "id":"group-1", "name":"target" }
            ]
            """;

            var page1 = """
            [
              { "id":"00000000-0000-0000-0000-000000000010" }
            ]
            """;

            var http = CreateHttpClient(
                _ => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"access_token\":\"admintoken\"}", Encoding.UTF8, "application/json")
                },
                _ => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(groupsSearchJson, Encoding.UTF8, "application/json")
                },
                _ => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(page1, Encoding.UTF8, "application/json")
                },
                _ => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("[]", Encoding.UTF8, "application/json")
                }
            );

            var svc = new KeycloakAdminService(http, CreateDefaultConfig());

            var ids = await svc.GetUserIdsInGroupAsync("target", CancellationToken.None);

            Assert.Single(ids);
        }

        [Fact]
        public async Task GetRealmRolesBulkAsync_ReturnsRolesForUsers()
        {
            var id1 = Guid.NewGuid();
            var id2 = Guid.NewGuid();

            var rolesJson1 = """
            [
              { "name":"admin" },
              { "name":"mentor" }
            ]
            """;

            var rolesJson2 = """
            [
              { "name":"intern" }
            ]
            """;

            var http = CreateHttpClient(
                _ => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"access_token\":\"admintoken\"}", Encoding.UTF8, "application/json")
                },
                _ => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(rolesJson1, Encoding.UTF8, "application/json")
                },
                _ => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(rolesJson2, Encoding.UTF8, "application/json")
                }
            );

            var svc = new KeycloakAdminService(http, CreateDefaultConfig());

            var map = await svc.GetRealmRolesBulkAsync(new[] { id1, id2 }, CancellationToken.None);

            Assert.Contains("admin", map[id1]);
            Assert.Contains("mentor", map[id1]);
            Assert.Contains("intern", map[id2]);
        }

        [Fact]
        public async Task GetGroupsBulkAsync_ReturnsGroupsForUsers()
        {
            var id1 = Guid.NewGuid();
            var id2 = Guid.NewGuid();

            var groupsJson1 = """
            [
              { "name":"g1" },
              { "name":"g2" }
            ]
            """;

            var groupsJson2 = """
            [
              { "name":"g3" }
            ]
            """;

            var http = CreateHttpClient(
                _ => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"access_token\":\"admintoken\"}", Encoding.UTF8, "application/json")
                },
                _ => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(groupsJson1, Encoding.UTF8, "application/json")
                },
                _ => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(groupsJson2, Encoding.UTF8, "application/json")
                }
            );

            var svc = new KeycloakAdminService(http, CreateDefaultConfig());

            var map = await svc.GetGroupsBulkAsync(new[] { id1, id2 }, CancellationToken.None);

            Assert.Contains("g1", map[id1]);
            Assert.Contains("g2", map[id1]);
            Assert.Contains("g3", map[id2]);
        }

        [Fact]
        public async Task DeleteUserAsync_HandlesSuccessAnd404AsTrue()
        {
            var http = CreateHttpClient(
                _ => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"access_token\":\"admintoken\"}", Encoding.UTF8, "application/json")
                },
                _ => new HttpResponseMessage(HttpStatusCode.NoContent),
                _ => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"access_token\":\"admintoken\"}", Encoding.UTF8, "application/json")
                },
                _ => new HttpResponseMessage(HttpStatusCode.NotFound)
            );

            var svc = new KeycloakAdminService(http, CreateDefaultConfig());

            var ok1 = await svc.DeleteUserAsync(Guid.NewGuid(), CancellationToken.None);
            var ok2 = await svc.DeleteUserAsync(Guid.NewGuid(), CancellationToken.None);

            Assert.True(ok1);
            Assert.True(ok2);
        }

        [Fact]
        public async Task CreateUserAsync_SuccessWithGroupJoin()
        {
            var newId = Guid.NewGuid();

            var http = CreateHttpClient(
                _ => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"access_token\":\"admintoken\"}", Encoding.UTF8, "application/json")
                },
                _ =>
                {
                    var resp = new HttpResponseMessage(HttpStatusCode.Created);
                    resp.Headers.Location = new Uri($"http://kc.local/admin/realms/TestRealm/users/{newId}");
                    resp.Content = new StringContent("", Encoding.UTF8, "application/json");
                    return resp;
                },
                _ => new HttpResponseMessage(HttpStatusCode.NoContent),
                _ => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""
                    [
                      { "id":"group-id", "name":"guest" }
                    ]
                    """, Encoding.UTF8, "application/json")
                },
                _ => new HttpResponseMessage(HttpStatusCode.NoContent)
            );

            var svc = new KeycloakAdminService(http, CreateDefaultConfig());

            var id = await svc.CreateUserAsync("user", "e@test.local", "John Doe", "pass", "guest", CancellationToken.None);

            Assert.Equal(newId, id);
        }

        [Fact]
        public async Task CreateUserAsync_Conflict_ThrowsConflictException()
        {
            var http = CreateHttpClient(
                _ => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"access_token\":\"admintoken\"}", Encoding.UTF8, "application/json")
                },
                _ => new HttpResponseMessage(HttpStatusCode.Conflict)
                {
                    Content = new StringContent("conflict", Encoding.UTF8, "application/json")
                }
            );

            var svc = new KeycloakAdminService(http, CreateDefaultConfig());

            await Assert.ThrowsAsync<ConflictException>(() =>
                svc.CreateUserAsync("u", "e@test.local", "Name", "pass", "guest", CancellationToken.None));
        }

        [Fact]
        public async Task CreateUserAsync_InvalidLocation_Throws()
        {
            var http = CreateHttpClient(
                _ => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"access_token\":\"admintoken\"}", Encoding.UTF8, "application/json")
                },
                _ =>
                {
                    var resp = new HttpResponseMessage(HttpStatusCode.Created);
                    resp.Headers.Location = new Uri("http://kc.local/admin/realms/TestRealm/users/not-a-guid");
                    resp.Content = new StringContent("", Encoding.UTF8, "application/json");
                    return resp;
                }
            );

            var svc = new KeycloakAdminService(http, CreateDefaultConfig());

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                svc.CreateUserAsync("u", "e@test.local", "Name", "pass", "guest", CancellationToken.None));
        }

        [Fact]
        public async Task CreateUserAsync_ResetPasswordFails_Throws()
        {
            var newId = Guid.NewGuid();

            var http = CreateHttpClient(
                _ => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"access_token\":\"admintoken\"}", Encoding.UTF8, "application/json")
                },
                _ =>
                {
                    var resp = new HttpResponseMessage(HttpStatusCode.Created);
                    resp.Headers.Location = new Uri($"http://kc.local/admin/realms/TestRealm/users/{newId}");
                    resp.Content = new StringContent("", Encoding.UTF8, "application/json");
                    return resp;
                },
                _ => new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Content = new StringContent("pwd fail", Encoding.UTF8, "application/json")
                }
            );

            var svc = new KeycloakAdminService(http, CreateDefaultConfig());

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                svc.CreateUserAsync("u", "e@test.local", "Name", "pass", "guest", CancellationToken.None));
        }

        [Fact]
        public async Task CreateUserAsync_JoinGroupFails_Throws()
        {
            var newId = Guid.NewGuid();

            var http = CreateHttpClient(
                _ => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"access_token\":\"admintoken\"}", Encoding.UTF8, "application/json")
                },
                _ =>
                {
                    var resp = new HttpResponseMessage(HttpStatusCode.Created);
                    resp.Headers.Location = new Uri($"http://kc.local/admin/realms/TestRealm/users/{newId}");
                    resp.Content = new StringContent("", Encoding.UTF8, "application/json");
                    return resp;
                },
                _ => new HttpResponseMessage(HttpStatusCode.NoContent),
                _ => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""
                    [
                      { "id":"group-id", "name":"guest" }
                    ]
                    """, Encoding.UTF8, "application/json")
                },
                _ => new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Content = new StringContent("join fail", Encoding.UTF8, "application/json")
                }
            );

            var svc = new KeycloakAdminService(http, CreateDefaultConfig());

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                svc.CreateUserAsync("u", "e@test.local", "Name", "pass", "guest", CancellationToken.None));
        }

        [Fact]
        public async Task PasswordTokenAsync_LoginFails_UserHasUpdatePasswordRequiredAction_ThrowsPasswordChangeRequired()
        {
            var userId = Guid.NewGuid();

            var http = CreateHttpClient(
                r => new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Content = new StringContent(
                        "{\"error\":\"invalid_grant\",\"error_description\":\"bad credentials\"}",
                        Encoding.UTF8,
                        "application/json")
                },
                r => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        "{\"access_token\":\"admintoken\"}",
                        Encoding.UTF8,
                        "application/json")
                },
                r => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        $"[{{\"id\":\"{userId}\"}}]",
                        Encoding.UTF8,
                        "application/json")
                },
                r => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        "{\"requiredActions\":[\"UPDATE_PASSWORD\"]}",
                        Encoding.UTF8,
                        "application/json")
                }
            );

            var svc = new KeycloakAdminService(http, CreateDefaultConfig());

            await Assert.ThrowsAsync<PasswordChangeRequiredException>(() =>
                svc.PasswordTokenAsync("user@example.com", "pwd", CancellationToken.None));
        }

        [Fact]
        public async Task PasswordTokenAsync_LoginFails_UserHasNoRequiredActions_ThrowsInvalidOperation()
        {
            var userId = Guid.NewGuid();

            var http = CreateHttpClient(
                r => new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Content = new StringContent(
                        "{\"error\":\"invalid_grant\",\"error_description\":\"bad credentials\"}",
                        Encoding.UTF8,
                        "application/json")
                },
                r => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        "{\"access_token\":\"admintoken\"}",
                        Encoding.UTF8,
                        "application/json")
                },
                r => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        $"[{{\"id\":\"{userId}\"}}]",
                        Encoding.UTF8,
                        "application/json")
                },
                r => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        "{\"requiredActions\":[\"SOMETHING_ELSE\"]}",
                        Encoding.UTF8,
                        "application/json")
                }
            );

            var svc = new KeycloakAdminService(http, CreateDefaultConfig());

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                svc.PasswordTokenAsync("user@example.com", "pwd", CancellationToken.None));
        }
        [Fact]
        public async Task PasswordTokenAsync_LoginFails_UserFoundByEmail_UserDetailsNotSuccess_ThrowsInvalidOperation()
        {
            var userId = Guid.NewGuid();

            var http = CreateHttpClient(
                r => new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Content = new StringContent(
                        "{\"error\":\"invalid_grant\",\"error_description\":\"bad credentials\"}",
                        Encoding.UTF8,
                        "application/json")
                },
                r => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        "{\"access_token\":\"admintoken\"}",
                        Encoding.UTF8,
                        "application/json")
                },
                r => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("[]", Encoding.UTF8, "application/json")
                },
                r => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        $"[{{\"id\":\"{userId}\"}}]",
                        Encoding.UTF8,
                        "application/json")
                },
                r => new HttpResponseMessage(HttpStatusCode.NotFound)
                {
                    Content = new StringContent("{}", Encoding.UTF8, "application/json")
                }
            );

            var svc = new KeycloakAdminService(http, CreateDefaultConfig());

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                svc.PasswordTokenAsync("user@example.com", "pwd", CancellationToken.None));
        }

        [Fact]
        public async Task PasswordTokenAsync_ErrorDescriptionContainsUpdatePassword_ThrowsPasswordChangeRequired()
        {
            var body = "{\"error\":\"invalid_grant\",\"error_description\":\"User must perform update_password now\"}";

            var http = CreateHttpClient(
                r => new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json")
                }
            );

            var svc = new KeycloakAdminService(http, CreateDefaultConfig());

            await Assert.ThrowsAsync<PasswordChangeRequiredException>(() =>
                svc.PasswordTokenAsync("user@example.com", "pwd", CancellationToken.None));
        }

        [Fact]
        public async Task PasswordTokenAsync_ErrorBodyContainsResolveRequiredActions_ThrowsPasswordChangeRequired()
        {
            var body = "{\"error\":\"some_error\",\"error_description\":\"please resolve_required_actions before login\"}";

            var http = CreateHttpClient(
                r => new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json")
                }
            );

            var svc = new KeycloakAdminService(http, CreateDefaultConfig());

            await Assert.ThrowsAsync<PasswordChangeRequiredException>(() =>
                svc.PasswordTokenAsync("user@example.com", "pwd", CancellationToken.None));
        }

        [Fact]
        public async Task PasswordTokenAsync_InvalidGrantWithNotFullySetUp_ThrowsPasswordChangeRequired()
        {
            var body = "{\"error\":\"invalid_grant\",\"error_description\":\"user not fully set up yet\"}";

            var http = CreateHttpClient(
                r => new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json")
                }
            );

            var svc = new KeycloakAdminService(http, CreateDefaultConfig());

            await Assert.ThrowsAsync<PasswordChangeRequiredException>(() =>
                svc.PasswordTokenAsync("user@example.com", "pwd", CancellationToken.None));
        }

        [Fact]
        public async Task PasswordTokenAsync_NonJsonBodyWithHints_ThrowsPasswordChangeRequired()
        {
            var body = "account is not fully set up and update_password required resolve_required_actions";

            var http = CreateHttpClient(
                r => new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Content = new StringContent(body, Encoding.UTF8, "text/plain")
                }
            );

            var svc = new KeycloakAdminService(http, CreateDefaultConfig());

            await Assert.ThrowsAsync<PasswordChangeRequiredException>(() =>
                svc.PasswordTokenAsync("user@example.com", "pwd", CancellationToken.None));
        }

        [Fact]
        public async Task CreateUserAsync_NoGroupAndRoleNotFound_DoesNotPostRoleMapping()
        {
            var createdUserId = Guid.NewGuid();
            var hadRoleMappingPost = false;

            var http = CreateHttpClient(
                r => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        "{\"access_token\":\"admintoken\"}",
                        Encoding.UTF8,
                        "application/json")
                },
                r =>
                {
                    var resp = new HttpResponseMessage(HttpStatusCode.Created);
                    resp.Headers.Location =
                        new Uri($"http://localhost/admin/realms/ApLabRealm/users/{createdUserId}");
                    resp.Content = new StringContent("{}", Encoding.UTF8, "application/json");
                    return resp;
                },
                r => new HttpResponseMessage(HttpStatusCode.NoContent),
                r => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("[]", Encoding.UTF8, "application/json")
                },
                r => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("[]", Encoding.UTF8, "application/json")
                },
                r => new HttpResponseMessage(HttpStatusCode.NotFound)
                {
                    Content = new StringContent("{}", Encoding.UTF8, "application/json")
                },
                r =>
                {
                    if (r.Method == HttpMethod.Post &&
                        r.RequestUri!.AbsolutePath.Contains("/role-mappings/realm", StringComparison.OrdinalIgnoreCase))
                    {
                        hadRoleMappingPost = true;
                    }

                    return new HttpResponseMessage(HttpStatusCode.NoContent);
                }
            );

            var svc = new KeycloakAdminService(http, CreateDefaultConfig());

            var id = await svc.CreateUserAsync(
                "user",
                "user@example.com",
                "Full Name",
                "Pass1!",
                "mentor",
                CancellationToken.None);

            Assert.Equal(createdUserId, id);
            Assert.False(hadRoleMappingPost);
        }

        
    }

    internal class QueueMessageHandler : HttpMessageHandler
    {
        private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _responses;

        public QueueMessageHandler(IEnumerable<Func<HttpRequestMessage, HttpResponseMessage>> responses)
        {
            _responses = new Queue<Func<HttpRequestMessage, HttpResponseMessage>>(responses);
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (_responses.Count == 0)
                throw new InvalidOperationException("No more fake responses configured for HttpClient.");

            var factory = _responses.Dequeue();
            var resp = factory(request);
            return Task.FromResult(resp);
        }
    }
}
