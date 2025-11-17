using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using APLabApp.Dal.Entities;
using Microsoft.EntityFrameworkCore;

namespace APLabApp.Dal.Repositories
{
    public class GradeRepository : IGradeRepository
    {
        private readonly AppDbContext _db;
        public GradeRepository(AppDbContext db) => _db = db;

        public IQueryable<Grade> Query() => _db.Grades;

        public Task<List<Grade>> GetAllAsync(CancellationToken ct) =>
            _db.Grades.AsNoTracking().ToListAsync(ct);

        public Task<Grade?> GetByFeedbackIdAsync(int feedbackId, CancellationToken ct) =>
            _db.Grades.FirstOrDefaultAsync(x => x.FeedbackId == feedbackId, ct);

        public Task AddAsync(Grade grade, CancellationToken ct) =>
            _db.Grades.AddAsync(grade, ct).AsTask();

        public Task UpdateAsync(Grade grade, CancellationToken ct)
        {
            _db.Grades.Update(grade);
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Grade grade, CancellationToken ct)
        {
            _db.Grades.Remove(grade);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
    }
}
