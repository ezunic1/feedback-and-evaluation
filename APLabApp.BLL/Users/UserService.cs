using APLabApp.BLL.Auth;
using APLabApp.BLL.Errors;
using APLabApp.Dal.Entities;
using APLabApp.Dal.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace APLabApp.BLL.Users
{
    public class UserService : IUserService
    {
        private readonly IUserRepository _repo;
        private readonly ISeasonRepository _seasons;
        private readonly IKeycloakAdminService _kc;
        private readonly bool _useEmailAsUsername;

        public UserService(IUserRepository repo, ISeasonRepository seasons, IKeycloakAdminService kc, IConfiguration cfg)
        {
            _repo = repo;
            _seasons = seasons;
            _kc = kc;
            _useEmailAsUsername = (cfg["Keycloak:UseEmailAsUsername"] ?? Environment.GetEnvironmentVariable("Keycloak__UseEmailAsUsername"))?
                                  .Equals("true", StringComparison.OrdinalIgnoreCase) == true;
        }

        public async Task<PagedResult<UserListItemDto>> GetPagedAsync(UsersQuery q, CancellationToken ct)
        {
            var page = Math.Max(1, q.Page);
            var size = Math.Clamp(q.PageSize, 1, 100);

            var query = _repo.Query().AsNoTracking();
            if (!string.IsNullOrWhiteSpace(q.Q))
            {
                var term = q.Q.Trim().ToLower();
                query = query.Where(u =>
                    (u.FullName ?? "").ToLower().Contains(term) ||
                    u.Email.ToLower().Contains(term));
            }
            if (q.From.HasValue) query = query.Where(u => u.CreatedAtUtc >= q.From!.Value);
            if (q.To.HasValue) query = query.Where(u => u.CreatedAtUtc <= q.To!.Value);
            if (q.SeasonId.HasValue && string.Equals(q.Role, "intern", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(u => u.SeasonId == q.SeasonId.Value);
            }
            query = (q.SortBy?.ToLower(), q.SortDir?.ToLower()) switch
            {
                ("name", "asc") => query.OrderBy(u => u.FullName).ThenBy(u => u.Id),
                ("name", _) => query.OrderByDescending(u => u.FullName).ThenByDescending(u => u.Id),
                ("email", "asc") => query.OrderBy(u => u.Email).ThenBy(u => u.Id),
                ("email", _) => query.OrderByDescending(u => u.Email).ThenByDescending(u => u.Id),
                _ when string.Equals(q.SortDir, "asc", StringComparison.OrdinalIgnoreCase)
                    => query.OrderBy(u => u.CreatedAtUtc).ThenBy(u => u.Id),
                _ => query.OrderByDescending(u => u.CreatedAtUtc).ThenByDescending(u => u.Id)
            };

            if (!string.IsNullOrWhiteSpace(q.Role))
            {
                var role = q.Role!.Trim().ToLower();
                var idsInRole = await _kc.GetUserIdsInRealmRoleAsync(role, ct);
                var idsInGroup = await _kc.GetUserIdsInGroupAsync(role, ct);
                var idsUnion = new HashSet<Guid>(idsInRole);
                idsUnion.UnionWith(idsInGroup);
                if (idsUnion.Count > 0)
                    query = query.Where(u => idsUnion.Contains(u.KeycloakId));
                else
                    query = query.Where(u => false);
            }

            var total = await query.CountAsync(ct);

            var pageEntities = await query
                .Skip((page - 1) * size)
                .Take(size)
                .ToListAsync(ct);

            var keycloakIds = pageEntities.Select(x => x.KeycloakId).ToList();
            var rolesMap = await _kc.GetRealmRolesBulkAsync(keycloakIds, ct);
            var groupsMap = await _kc.GetGroupsBulkAsync(keycloakIds, ct);

            static string PickRoleLocal(string[] roles, string[] groups)
            {
                var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (roles != null) foreach (var r in roles) if (!string.IsNullOrWhiteSpace(r)) set.Add(r);
                if (groups != null) foreach (var g in groups) if (!string.IsNullOrWhiteSpace(g)) set.Add(g);
                if (set.Contains("admin")) return "admin";
                if (set.Contains("mentor")) return "mentor";
                if (set.Contains("intern")) return "intern";
                if (set.Contains("guest")) return "guest";
                return "guest";
            }

            var items = pageEntities.Select(u =>
            {
                rolesMap.TryGetValue(u.KeycloakId, out var rr);
                groupsMap.TryGetValue(u.KeycloakId, out var gg);
                var role = PickRoleLocal(rr ?? Array.Empty<string>(), gg ?? Array.Empty<string>());
                return new UserListItemDto(
                    u.Id,
                    u.FullName,
                    u.Email,
                    role,
                    u.CreatedAtUtc
                );
            }).ToList();

            var totalPages = (int)Math.Ceiling(total / (double)size);
            return new PagedResult<UserListItemDto>(items, page, size, total, totalPages);
        }

        public async Task<IReadOnlyList<UserDto>> GetAllAsync(CancellationToken ct)
            => (await _repo.GetAllAsync(ct)).Select(UserMappings.FromEntity).ToList();

        public async Task<UserDto?> GetByIdAsync(Guid id, CancellationToken ct)
        {
            var e = await _repo.GetByIdAsync(id, ct);
            if (e is null) return null;

            var dto = UserMappings.FromEntity(e);

            string? seasonName = null;
            if (e.SeasonId.HasValue)
            {
                var s = await _seasons.GetByIdAsync(e.SeasonId.Value, ct);
                seasonName = s?.Name;
            }

            if (e.KeycloakId != Guid.Empty)
            {
                var rrMap = await _kc.GetRealmRolesBulkAsync(new[] { e.KeycloakId }, ct);
                var ggMap = await _kc.GetGroupsBulkAsync(new[] { e.KeycloakId }, ct);
                rrMap.TryGetValue(e.KeycloakId, out var rr);
                ggMap.TryGetValue(e.KeycloakId, out var gg);
                var role = PickRole(rr ?? Array.Empty<string>(), gg ?? Array.Empty<string>());
                dto = dto with { RoleName = role, SeasonName = seasonName };
            }
            else
            {
                dto = dto with { RoleName = "guest", SeasonName = seasonName };
            }

            return dto;
        }

        public async Task<UserDto?> GetByKeycloakIdAsync(Guid keycloakId, CancellationToken ct)
        {
            var all = await _repo.GetAllAsync(ct);
            var e = all.FirstOrDefault(u => u.KeycloakId == keycloakId);
            if (e is null) return null;

            var dto = UserMappings.FromEntity(e);

            string? seasonName = null;
            if (e.SeasonId.HasValue)
            {
                var s = await _seasons.GetByIdAsync(e.SeasonId.Value, ct);
                seasonName = s?.Name;
            }

            if (e.KeycloakId != Guid.Empty)
            {
                var rrMap = await _kc.GetRealmRolesBulkAsync(new[] { e.KeycloakId }, ct);
                var ggMap = await _kc.GetGroupsBulkAsync(new[] { e.KeycloakId }, ct);
                rrMap.TryGetValue(e.KeycloakId, out var rr);
                ggMap.TryGetValue(e.KeycloakId, out var gg);
                var role = PickRole(rr ?? Array.Empty<string>(), gg ?? Array.Empty<string>());
                dto = dto with { RoleName = role, SeasonName = seasonName };
            }
            else
            {
                dto = dto with { RoleName = "guest", SeasonName = seasonName };
            }

            return dto;
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
            var password = string.IsNullOrWhiteSpace(req.Password) ? "ChangeMe123!" : req.Password!;
            var keycloakId = await _kc.CreateUserAsync(username, email, req.FullName, password, role, ct, req.ForcePasswordChange);
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
            var oldName = e.FullName;
            req.Apply(e);
            if (!string.IsNullOrWhiteSpace(role) && e.KeycloakId != Guid.Empty)
                await _kc.ReplaceGroupsWithAsync(e.KeycloakId, role!, ct);
            if (role is not null && role != "intern")
                e.SeasonId = null;
            if (e.KeycloakId != Guid.Empty && !string.Equals(oldName, e.FullName, StringComparison.Ordinal))
            {
                var (first, last) = SplitName(e.FullName);
                await _kc.UpdateUserProfileAsync(e.KeycloakId, first, last, null, ct);
            }
            await _repo.UpdateAsync(e, ct);
            await _repo.SaveChangesAsync(ct);
            return UserMappings.FromEntity(e);
        }

        public async Task<UserDto?> UpdateSelfAsync(Guid keycloakId, string fullName, string? description, CancellationToken ct)
        {
            var all = await _repo.GetAllAsync(ct);
            var e = all.FirstOrDefault(u => u.KeycloakId == keycloakId);
            if (e is null) return null;
            var newName = (fullName ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(newName))
            {
                e.FullName = newName;
                var (first, last) = SplitName(newName);
                await _kc.UpdateUserProfileAsync(keycloakId, first, last, null, ct);
            }
            e.Desc = description;
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
            if (string.IsNullOrWhiteSpace(req.FullName))
                throw new ValidationException("Full name is required.");
            if (string.IsNullOrWhiteSpace(req.Email))
                throw new ValidationException("Email is required.");

            var email = req.Email.Trim().ToLowerInvariant();

            var emailExists = await _repo.Query()
                .AsNoTracking()
                .AnyAsync(u => u.Email.ToLower() == email, ct);
            if (emailExists)
                throw new ConflictException("A user with this email already exists.");

            var username = BuildUsernameFromEmail(email); 

            Guid? keycloakId;
            try
            {
                keycloakId = await _kc.CreateUserAsync(username, email, req.FullName, password, "guest", ct, false);
            }
            catch (ConflictException)
            {
                // Keycloak javlja da username ili email već postoji u IdP-u
                throw;
            }

            if (keycloakId is null)
                throw new ConflictException("Identity provider did not return a user id.");

            var entity = req.ToEntity();
            entity.Email = email;
            entity.KeycloakId = keycloakId.Value;

            await _repo.AddAsync(entity, ct);
            await _repo.SaveChangesAsync(ct);

            return UserMappings.FromEntity(entity);
        }

        public async Task<UserDto> EnsureLocalUserAsync(Guid keycloakId, string? email, string? fullName, CancellationToken ct)
        {
            var exists = await _repo.ExistsByKeycloakIdAsync(keycloakId, ct);
            if (exists)
            {
                var all = await _repo.GetAllAsync(ct);
                var e = all.First(x => x.KeycloakId == keycloakId);
                return UserMappings.FromEntity(e);
            }
            var name = string.IsNullOrWhiteSpace(fullName) ? (email ?? "User") : fullName;
            var eNew = new User
            {
                Id = Guid.NewGuid(),
                FullName = name,
                Email = email ?? string.Empty,
                KeycloakId = keycloakId
            };
            await _repo.AddAsync(eNew, ct);
            await _repo.SaveChangesAsync(ct);
            return UserMappings.FromEntity(eNew);
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

        private static (string first, string last) SplitName(string full)
        {
            var parts = (full ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return (full ?? string.Empty, "");
            if (parts.Length == 1) return (parts[0], "");
            return (parts[0], string.Join(' ', parts.Skip(1)));
        }

        private static string PickRole(IEnumerable<string>? roles, IEnumerable<string>? groups)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (roles != null) foreach (var r in roles) if (!string.IsNullOrWhiteSpace(r)) set.Add(r);
            if (groups != null) foreach (var g in groups) if (!string.IsNullOrWhiteSpace(g)) set.Add(g);
            if (set.Contains("admin")) return "admin";
            if (set.Contains("mentor")) return "mentor";
            if (set.Contains("intern")) return "intern";
            if (set.Contains("guest")) return "guest";
            return "guest";
        }

        private static string BuildUsernameFromEmail(string email)
        {
            var sb = new StringBuilder(email.Length);
            foreach (var ch in email.ToLowerInvariant())
            {
                if ((ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9') || ch == '.' || ch == '_' || ch == '-')
                    sb.Append(ch);
                else if (ch == '@')
                    sb.Append("__at__");
                else
                    sb.Append('_');
            }
            var candidate = sb.ToString().Trim('_', '.', '-');
            if (string.IsNullOrWhiteSpace(candidate) || candidate.Length < 3)
                candidate = "user_" + Guid.NewGuid().ToString("N")[..8];
            return candidate;
        }
    }
}
