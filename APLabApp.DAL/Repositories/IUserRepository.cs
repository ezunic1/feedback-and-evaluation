using APLabApp.Dal.Entities;

namespace APLabApp.Dal.Repositories;

public interface IUserRepository
{
    Task<List<User>> GetAllAsync(CancellationToken ct);
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<bool> ExistsByKeycloakIdAsync(Guid keycloakId, CancellationToken ct);

    Task AddAsync(User user, CancellationToken ct);
    Task UpdateAsync(User user, CancellationToken ct);
    Task DeleteAsync(User user, CancellationToken ct);

    Task SaveChangesAsync(CancellationToken ct);
}
