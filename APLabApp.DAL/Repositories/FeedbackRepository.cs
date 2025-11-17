using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using APLabApp.Dal.Entities;
using Microsoft.EntityFrameworkCore;

namespace APLabApp.Dal.Repositories
{
    public class FeedbackRepository : IFeedbackRepository
    {
        private readonly AppDbContext _db;
        public FeedbackRepository(AppDbContext db) => _db = db;

        public IQueryable<Feedback> Query() => _db.Feedbacks;

        public Task<List<Feedback>> GetAllAsync(CancellationToken ct) =>
            _db.Feedbacks
                .AsNoTracking()
                .OrderByDescending(x => x.CreatedAtUtc)
                .ToListAsync(ct);

        public Task<Feedback?> GetByIdAsync(int id, CancellationToken ct) =>
            _db.Feedbacks.FirstOrDefaultAsync(x => x.Id == id, ct);

        public Task AddAsync(Feedback feedback, CancellationToken ct) =>
            _db.Feedbacks.AddAsync(feedback, ct).AsTask();

        public Task UpdateAsync(Feedback feedback, CancellationToken ct)
        {
            _db.Feedbacks.Update(feedback);
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Feedback feedback, CancellationToken ct)
        {
            _db.Feedbacks.Remove(feedback);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
    }
}
