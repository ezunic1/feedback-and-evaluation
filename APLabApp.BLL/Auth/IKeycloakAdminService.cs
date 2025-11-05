using System;
using System.Threading;
using System.Threading.Tasks;

namespace APLabApp.BLL.Auth
{
    public interface IKeycloakAdminService
    {
        Task<Guid?> CreateUserAsync(string username, string email, string fullName, string password, string role, CancellationToken ct);
        Task<bool> ResetPasswordAsync(Guid keycloakUserId, string newPassword, CancellationToken ct);
        Task<bool> VerifyUserPasswordAsync(string username, string password, CancellationToken ct);
        Task<bool> IsUserInGroupAsync(Guid keycloakUserId, string groupName, CancellationToken ct);
        Task ReplaceGroupsWithAsync(Guid keycloakUserId, string groupName, CancellationToken ct);
    }
}
