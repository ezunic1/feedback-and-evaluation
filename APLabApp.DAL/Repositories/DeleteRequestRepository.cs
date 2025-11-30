using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using APLabApp.Dal.Entities;
using Microsoft.EntityFrameworkCore;

namespace APLabApp.Dal.Repositories
{
    public class DeleteRequestRepository : IDeleteRequestRepository
    {
        private readonly AppDbContext _db;
        public DeleteRequestRepository(AppDbContext db) => _db = db;

        public Task<List<DeleteRequest>> GetAllAsync(CancellationToken ct) =>
            _db.DeleteRequests
               .AsNoTracking()
               .OrderByDescending(x => x.CreatedAtUtc)
               .ToListAsync(ct);

        public Task<DeleteRequest?> GetByIdAsync(int id, CancellationToken ct) =>
            _db.DeleteRequests.FirstOrDefaultAsync(x => x.Id == id, ct);

        public Task AddAsync(DeleteRequest request, CancellationToken ct) =>
            _db.DeleteRequests.AddAsync(request, ct).AsTask();

        public Task DeleteAsync(DeleteRequest request, CancellationToken ct)
        {
            _db.DeleteRequests.Remove(request);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
    }
}
