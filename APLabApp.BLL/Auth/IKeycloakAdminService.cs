using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace APLabApp.BLL.Auth
{
    public record TokenResponse(
        string access_token,
        string token_type,
        int expires_in,
        string refresh_token,
        int refresh_expires_in,
        string? scope
    );

    public interface IKeycloakAdminService
    {
        Task<Guid?> CreateUserAsync(string username, string email, string fullName, string password, string role, CancellationToken ct);
        Task<bool> ResetPasswordAsync(Guid keycloakUserId, string newPassword, CancellationToken ct);
        Task<bool> VerifyUserPasswordAsync(string usernameOrEmail, string password, CancellationToken ct);
        Task<bool> IsUserInGroupAsync(Guid keycloakUserId, string groupName, CancellationToken ct);
        Task ReplaceGroupsWithAsync(Guid keycloakUserId, string groupName, CancellationToken ct);

        // login (password grant)
        Task<TokenResponse> PasswordTokenAsync(string usernameOrEmail, string password, CancellationToken ct);
    }
}
