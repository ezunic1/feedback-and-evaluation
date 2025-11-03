namespace APLabApp.BLL.Auth;

public interface IKeycloakAdminService
{
    Task<bool> ResetPasswordAsync(Guid keycloakUserId, string newPassword, CancellationToken ct);
    Task<bool> VerifyUserPasswordAsync(string username, string password, CancellationToken ct);
}
