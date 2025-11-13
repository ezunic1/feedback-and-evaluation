using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace APLabApp.BLL.Auth
{
    public interface IKeycloakAdminService
    {
        Task<Guid?> CreateUserAsync(string username, string email, string fullName, string password, string role, CancellationToken ct, bool temporaryPassword = false);
        Task ReplaceGroupsWithAsync(Guid keycloakUserId, string role, CancellationToken ct);
        Task<bool> UpdateUserProfileAsync(Guid keycloakUserId, string? firstName, string? lastName, IDictionary<string, string?>? attributes, CancellationToken ct);
        Task<bool> VerifyUserPasswordAsync(string usernameOrEmail, string password, CancellationToken ct);
        Task<bool> ResetPasswordAsync(Guid keycloakUserId, string newPassword, CancellationToken ct);
        Task<bool> IsUserInGroupAsync(Guid keycloakUserId, string groupName, CancellationToken ct);
        Task<HashSet<Guid>> GetUserIdsInRealmRoleAsync(string role, CancellationToken ct);
        Task<bool> DeleteUserAsync(Guid keycloakUserId, CancellationToken ct);

        Task<HashSet<Guid>> GetUserIdsInGroupAsync(string groupName, CancellationToken ct);
        Task<Dictionary<Guid, string[]>> GetRealmRolesBulkAsync(IEnumerable<Guid> keycloakUserIds, CancellationToken ct);
        Task<Dictionary<Guid, string[]>> GetGroupsBulkAsync(IEnumerable<Guid> keycloakUserIds, CancellationToken ct);
        Task<TokenResponse> PasswordTokenAsync(string usernameOrEmail, string password, CancellationToken ct);
        string BuildBrowserAuthUrl(string? redirectUri, string? state = null);
    }
}
