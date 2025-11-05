using APLabApp.Dal.Entities;
using Microsoft.EntityFrameworkCore;

namespace APLabApp.Dal.Repositories
{
    public class SeasonRepository : ISeasonRepository
    {
        private readonly AppDbContext _db;

        public SeasonRepository(AppDbContext db) => _db = db;

        public Task<List<Season>> GetAllAsync(CancellationToken ct) =>
            _db.Seasons.AsNoTracking().Include(s => s.Mentor).ToListAsync(ct);

        public async Task<Season?> GetByIdAsync(int id, CancellationToken ct, bool includeUsers = false)
        {
            IQueryable<Season> q = _db.Seasons.Include(s => s.Mentor);
            if (includeUsers) q = q.Include(s => s.Users);
            return await q.FirstOrDefaultAsync(s => s.Id == id, ct);
        }

        public async Task AddAsync(Season season, CancellationToken ct)
        {
            await _db.Seasons.AddAsync(season, ct);
        }

        public Task UpdateAsync(Season season, CancellationToken ct)
        {
            _db.Seasons.Update(season);
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Season season, CancellationToken ct)
        {
            _db.Seasons.Remove(season);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
    }
}
