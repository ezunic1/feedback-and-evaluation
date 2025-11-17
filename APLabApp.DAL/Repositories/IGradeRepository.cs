using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using APLabApp.Dal.Entities;

namespace APLabApp.Dal.Repositories
{
    public interface IGradeRepository
    {
        IQueryable<Grade> Query();
        Task<List<Grade>> GetAllAsync(CancellationToken ct);
        Task<Grade?> GetByFeedbackIdAsync(int feedbackId, CancellationToken ct);

        Task AddAsync(Grade grade, CancellationToken ct);
        Task UpdateAsync(Grade grade, CancellationToken ct);
        Task DeleteAsync(Grade grade, CancellationToken ct);
        Task SaveChangesAsync(CancellationToken ct);
    }
}
