using APLabApp.Dal.Repositories;
using APLabApp.BLL.Auth;
using APLabApp.Dal.Entities;

namespace APLabApp.BLL.Users;

public class UserService : IUserService
{
    private readonly IUserRepository _repo;
    private readonly IKeycloakAdminService _kc;

    public UserService(IUserRepository repo, IKeycloakAdminService kc)
    {
        _repo = repo;
        _kc = kc;
    }

    public async Task<IReadOnlyList<UserDto>> GetAllAsync(CancellationToken ct)
        => (await _repo.GetAllAsync(ct)).Select(UserMappings.FromEntity).ToList();

    public async Task<UserDto?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var e = await _repo.GetByIdAsync(id, ct);
        return e is null ? null : UserMappings.FromEntity(e);
    }

    public async Task<UserDto> CreateAsync(CreateUserRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.FullName))
            throw new ArgumentException("FullName is required.");
        if (string.IsNullOrWhiteSpace(req.Email))
            throw new ArgumentException("Email is required.");

        var username = req.Email!.Split('@')[0];
        var defaultPassword = "ChangeMe123!";
        var defaultRole = "intern";

        var keycloakId = await _kc.CreateUserAsync(username, req.Email!, req.FullName, defaultPassword, defaultRole, ct);

        var e = req.ToEntity();
        e.KeycloakId = keycloakId ?? Guid.Empty;

        await _repo.AddAsync(e, ct);
        await _repo.SaveChangesAsync(ct);

        return UserMappings.FromEntity(e);
    }

    public async Task<UserDto?> UpdateAsync(Guid id, UpdateUserRequest req, CancellationToken ct)
    {
        var e = await _repo.GetByIdAsync(id, ct);
        if (e is null) return null;
        req.Apply(e);
        await _repo.UpdateAsync(e, ct);
        await _repo.SaveChangesAsync(ct);
        return UserMappings.FromEntity(e);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct)
    {
        var e = await _repo.GetByIdAsync(id, ct);
        if (e is null) return false;
        await _repo.DeleteAsync(e, ct);
        await _repo.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> ChangePasswordAsync(Guid id, string newPassword, string? currentPassword, CancellationToken ct)
    {
        var e = await _repo.GetByIdAsync(id, ct);
        if (e is null || e.KeycloakId == Guid.Empty) return false;

        if (!string.IsNullOrWhiteSpace(currentPassword))
        {
            if (string.IsNullOrWhiteSpace(e.Email)) return false;
            var ok = await _kc.VerifyUserPasswordAsync(e.Email!, currentPassword!, ct);
            if (!ok) return false;
        }

        return await _kc.ResetPasswordAsync(e.KeycloakId, newPassword, ct);
    }
}
