using APLabApp.Dal.Entities;
using Microsoft.EntityFrameworkCore;

namespace APLabApp.Dal.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly AppDbContext _db;
        public UserRepository(AppDbContext db) => _db = db;

        public IQueryable<User> Query() => _db.Users;

        public Task<List<User>> GetAllAsync(CancellationToken ct) =>
            _db.Users.AsNoTracking().OrderBy(x => x.FullName).ToListAsync(ct);

        public Task<User?> GetByIdAsync(Guid id, CancellationToken ct) =>
            _db.Users.FirstOrDefaultAsync(x => x.Id == id, ct);

        public Task<bool> ExistsByKeycloakIdAsync(Guid keycloakId, CancellationToken ct) =>
            _db.Users.AnyAsync(x => x.KeycloakId == keycloakId, ct);

        public Task AddAsync(User user, CancellationToken ct) =>
            _db.Users.AddAsync(user, ct).AsTask();

        public Task UpdateAsync(User user, CancellationToken ct)
        {
            _db.Users.Update(user);
            return Task.CompletedTask;
        }

        public Task DeleteAsync(User user, CancellationToken ct)
        {
            _db.Users.Remove(user);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
    }
}
