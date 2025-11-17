using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using APLabApp.Dal.Entities;

namespace APLabApp.Dal.Repositories
{
    public interface IFeedbackRepository
    {
        IQueryable<Feedback> Query();
        Task<List<Feedback>> GetAllAsync(CancellationToken ct);
        Task<Feedback?> GetByIdAsync(int id, CancellationToken ct);

        Task AddAsync(Feedback feedback, CancellationToken ct);
        Task UpdateAsync(Feedback feedback, CancellationToken ct);
        Task DeleteAsync(Feedback feedback, CancellationToken ct);
        Task SaveChangesAsync(CancellationToken ct);
    }
}
