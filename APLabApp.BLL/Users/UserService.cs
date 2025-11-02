using APLabApp.BLL.Users;
using APLabApp.Dal.Entities;
using APLabApp.Dal.Repositories;

namespace APLabApp.BLL;

public class UserService : IUserService
{
    private readonly IUserRepository _repo;
    public UserService(IUserRepository repo) => _repo = repo;

    public async Task<IReadOnlyList<UserDto>> GetAllAsync(CancellationToken ct)
        => (await _repo.GetAllAsync(ct)).Select(ToDto).ToList();

    public async Task<UserDto?> GetByIdAsync(Guid id, CancellationToken ct)
        => await _repo.GetByIdAsync(id, ct) is { } u ? ToDto(u) : null;

    public async Task<UserDto> CreateAsync(CreateUserRequest req, CancellationToken ct)
    {
        var u = new User
        {
            KeycloakId = req.KeycloakId,
            FullName = req.FullName.Trim(),
            Desc = req.Desc,
            Email = req.Email,
            CreatedAtUtc = DateTime.UtcNow
        };
        await _repo.AddAsync(u, ct);
        await _repo.SaveChangesAsync(ct);
        return ToDto(u);
    }

    public async Task<UserDto?> UpdateAsync(Guid id, UpdateUserRequest req, CancellationToken ct)
    {
        var u = await _repo.GetByIdAsync(id, ct);
        if (u is null) return null;

        if (!string.IsNullOrWhiteSpace(req.FullName)) u.FullName = req.FullName.Trim();
        u.Desc = req.Desc;
        u.Email = req.Email;

        await _repo.UpdateAsync(u, ct);
        await _repo.SaveChangesAsync(ct);
        return ToDto(u);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct)
    {
        var u = await _repo.GetByIdAsync(id, ct);
        if (u is null) return false;
        await _repo.DeleteAsync(u, ct);
        await _repo.SaveChangesAsync(ct);
        return true;
    }

    private static UserDto ToDto(User u) =>
        new(u.Id, u.KeycloakId, u.FullName, u.Desc, u.Email, u.CreatedAtUtc);
}
