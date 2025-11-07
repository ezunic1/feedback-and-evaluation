using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using APLabApp.Dal.Entities;
using Microsoft.EntityFrameworkCore;

namespace APLabApp.Dal.Repositories
{
    public class SeasonRepository : ISeasonRepository
    {
        private readonly AppDbContext _db;

        public SeasonRepository(AppDbContext db) => _db = db;

        public Task<List<Season>> GetAllAsync(CancellationToken ct) =>
            _db.Seasons
               .AsNoTracking()
               .Include(s => s.Mentor)
               .Include(s => s.Users)
               .ToListAsync(ct);

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

        public async Task<Season?> GetCurrentForInternAsync(Guid userId, DateTime utcNow, CancellationToken ct)
        {
            return await _db.Seasons
                .Include(s => s.Users)
                .Where(s => s.Users.Any(u => u.Id == userId)
                            && s.StartDate <= utcNow
                            && s.EndDate >= utcNow)
                .OrderByDescending(s => s.StartDate)
                .FirstOrDefaultAsync(ct);
        }

        public async Task<Season?> GetCurrentForMentorAsync(Guid mentorId, DateTime utcNow, CancellationToken ct)
        {
            return await _db.Seasons
                .Where(s => s.MentorId == mentorId
                            && s.StartDate <= utcNow
                            && s.EndDate >= utcNow)
                .OrderByDescending(s => s.StartDate)
                .FirstOrDefaultAsync(ct);
        }
    }
}
