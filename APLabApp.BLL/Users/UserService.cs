using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using APLabApp.Dal.Repositories;
using APLabApp.BLL.Auth;
using APLabApp.Dal.Entities;
using Microsoft.Extensions.Configuration;
using System.Text;

namespace APLabApp.BLL.Users
{
    public class UserService : IUserService
    {
        private readonly IUserRepository _repo;
        private readonly IKeycloakAdminService _kc;
        private readonly bool _useEmailAsUsername;

        public UserService(IUserRepository repo, IKeycloakAdminService kc, IConfiguration cfg)
        {
            _repo = repo;
            _kc = kc;
            _useEmailAsUsername = (cfg["Keycloak:UseEmailAsUsername"] ?? Environment.GetEnvironmentVariable("Keycloak__UseEmailAsUsername"))?
                                  .Equals("true", StringComparison.OrdinalIgnoreCase) == true;
        }

        public async Task<IReadOnlyList<UserDto>> GetAllAsync(CancellationToken ct)
            => (await _repo.GetAllAsync(ct)).Select(UserMappings.FromEntity).ToList();

        public async Task<UserDto?> GetByIdAsync(Guid id, CancellationToken ct)
        {
            var e = await _repo.GetByIdAsync(id, ct);
            return e is null ? null : UserMappings.FromEntity(e);
        }

        public async Task<UserDto?> GetByKeycloakIdAsync(Guid keycloakId, CancellationToken ct)
        {
            var all = await _repo.GetAllAsync(ct);
            var e = all.FirstOrDefault(u => u.KeycloakId == keycloakId);
            return e is null ? null : UserMappings.FromEntity(e);
        }

        public async Task<UserDto> CreateAsync(CreateUserRequest req, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(req.FullName)) throw new ArgumentException("FullName is required.");
            if (string.IsNullOrWhiteSpace(req.Email)) throw new ArgumentException("Email is required.");

            var email = req.Email.Trim().ToLowerInvariant();
            var username = BuildUsername(email, req.FullName);
            var role = string.IsNullOrWhiteSpace(req.RoleName) ? "guest" : req.RoleName!.Trim().ToLowerInvariant();

            if (role != "intern" && req.SeasonId.HasValue)
                throw new InvalidOperationException($"Users with role '{role}' cannot have SeasonId.");

            var keycloakId = await _kc.CreateUserAsync(username, email, req.FullName, "ChangeMe123!", role, ct);
            if (keycloakId is null) throw new InvalidOperationException("Keycloak user creation failed.");

            var e = req.ToEntity();
            e.KeycloakId = keycloakId.Value;

            await _repo.AddAsync(e, ct);
            await _repo.SaveChangesAsync(ct);

            return UserMappings.FromEntity(e);
        }

        public async Task<UserDto?> UpdateAsync(Guid id, UpdateUserRequest req, CancellationToken ct)
        {
            var e = await _repo.GetByIdAsync(id, ct);
            if (e is null) return null;

            var role = req.RoleName?.Trim().ToLowerInvariant();

            if (req.SeasonId.HasValue && role != "intern")
                throw new InvalidOperationException("SeasonId can only be set when RoleName is 'intern' in this request.");

            req.Apply(e);

            if (role is not null && role != "intern")
                e.SeasonId = null;

            await _repo.UpdateAsync(e, ct);
            await _repo.SaveChangesAsync(ct);
            return UserMappings.FromEntity(e);
        }

        public async Task<bool> DeleteAsync(Guid id, CancellationToken ct)
        {
            var e = await _repo.GetByIdAsync(id, ct);
            if (e is null) return false;
            await _repo.DeleteAsync(e, ct);
            await _repo.SaveChangesAsync(ct);
            return true;
        }

        public async Task<bool> ChangePasswordAsync(Guid id, string newPassword, string? currentPassword, CancellationToken ct)
        {
            var e = await _repo.GetByIdAsync(id, ct);
            if (e is null || e.KeycloakId == Guid.Empty) return false;

            if (!string.IsNullOrWhiteSpace(currentPassword))
            {
                if (string.IsNullOrWhiteSpace(e.Email)) return false;
                var ok = await _kc.VerifyUserPasswordAsync(e.Email, currentPassword, ct);
                if (!ok) return false;
            }

            return await _kc.ResetPasswordAsync(e.KeycloakId, newPassword, ct);
        }

        public async Task<UserDto> CreateGuestAsync(CreateUserRequest req, string password, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(req.Email)) throw new ArgumentException("Email is required.");

            var email = req.Email.Trim().ToLowerInvariant();
            var username = BuildUsername(email, req.FullName);

            var keycloakId = await _kc.CreateUserAsync(username, email, req.FullName, password, string.Empty, ct);
            if (keycloakId is null) throw new InvalidOperationException("Keycloak user creation failed.");

            var entity = req.ToEntity();
            entity.KeycloakId = keycloakId.Value;

            await _repo.AddAsync(entity, ct);
            await _repo.SaveChangesAsync(ct);

            return UserMappings.FromEntity(entity);
        }

        private string BuildUsername(string email, string fullName)
        {
            if (_useEmailAsUsername) return email;

            var local = email.Split('@')[0];
            var sb = new StringBuilder(local.Length);
            foreach (var ch in local.ToLowerInvariant())
            {
                if ((ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9') || ch == '.' || ch == '_' || ch == '-')
                    sb.Append(ch);
            }
            var candidate = sb.ToString().Trim('-', '_', '.');
            if (string.IsNullOrWhiteSpace(candidate) || candidate.Length < 3)
                candidate = "user_" + Guid.NewGuid().ToString("N")[..8];
            return candidate;
        }
    }
}
