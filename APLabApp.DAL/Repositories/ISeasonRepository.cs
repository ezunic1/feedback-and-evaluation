using APLabApp.Dal.Entities;

namespace APLabApp.Dal.Repositories
{
    public interface ISeasonRepository
    {
        Task<List<Season>> GetAllAsync(CancellationToken ct);
        Task<Season?> GetByIdAsync(int id, CancellationToken ct, bool includeUsers = false);
        Task AddAsync(Season season, CancellationToken ct);
        Task UpdateAsync(Season season, CancellationToken ct);
        Task DeleteAsync(Season season, CancellationToken ct);
        Task SaveChangesAsync(CancellationToken ct);
    }
}
