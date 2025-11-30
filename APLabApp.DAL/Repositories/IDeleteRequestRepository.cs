using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using APLabApp.Dal.Entities;

namespace APLabApp.Dal.Repositories
{
    public interface IDeleteRequestRepository
    {
        Task<List<DeleteRequest>> GetAllAsync(CancellationToken ct);
        Task<DeleteRequest?> GetByIdAsync(int id, CancellationToken ct);
        Task AddAsync(DeleteRequest request, CancellationToken ct);
        Task DeleteAsync(DeleteRequest request, CancellationToken ct);
        Task SaveChangesAsync(CancellationToken ct);
    }
}
