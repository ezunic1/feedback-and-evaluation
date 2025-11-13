using APLabApp.BLL.Auth;
using APLabApp.BLL.Errors;
using APLabApp.BLL.Users;
using APLabApp.Dal.Entities;
using APLabApp.Dal.Repositories;

namespace APLabApp.BLL.Seasons
{
    public class SeasonService : ISeasonService
    {
        private readonly ISeasonRepository _seasons;
        private readonly IUserRepository _users;
        private readonly IKeycloakAdminService _kc;

        public SeasonService(ISeasonRepository seasons, IUserRepository users, IKeycloakAdminService kc)
        {
            _seasons = seasons;
            _users = users;
            _kc = kc;
        }

        public async Task<IReadOnlyList<SeasonDto>> GetAllAsync(CancellationToken ct)
        {
            var list = await _seasons.GetAllAsync(ct);
            return list.Select(SeasonMappings.FromEntity).ToList();
        }

        public async Task<SeasonDto?> GetByIdAsync(int id, bool includeUsers, CancellationToken ct)
        {
            var s = await _seasons.GetByIdAsync(id, ct, includeUsers);
            return s is null ? null : SeasonMappings.FromEntity(s);
        }

        public async Task<SeasonDto> CreateAsync(CreateSeasonRequest req, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(req.Name))
                throw new ValidationException("Season name is required.");

            if (req.StartDate == default || req.EndDate == default)
                throw new ValidationException("Start and end date are required.");

            if (req.StartDate >= req.EndDate)
                throw new ValidationException("Start date must be before end date.");

            // provjera preklapanja sa postojećim sezonama
            var allSeasons = await _seasons.GetAllAsync(ct); 

            var overlapping = allSeasons
                .FirstOrDefault(s =>
                    s.StartDate <= req.EndDate &&
                    s.EndDate >= req.StartDate);

            if (overlapping is not null)
            {
                throw new ConflictException(
                    $"Season '{overlapping.Name}' ({overlapping.StartDate:yyyy-MM-dd} – {overlapping.EndDate:yyyy-MM-dd}) already occupies this period.");
            }

            if (req.MentorId.HasValue)
            {
                var mentor = await _users.GetByIdAsync(req.MentorId.Value, ct);
                if (mentor is null)
                    throw new NotFoundException("Mentor not found.");

                if (mentor.KeycloakId == Guid.Empty)
                    throw new ValidationException("Mentor has no Keycloak link.");

                var isMentor = await _kc.IsUserInGroupAsync(mentor.KeycloakId, "mentor", ct);
                if (!isMentor)
                    throw new ValidationException("Selected user is not a mentor.");
            }

            var entity = new Season
            {
                Name = req.Name.Trim(),
                StartDate = req.StartDate,
                EndDate = req.EndDate,
                MentorId = req.MentorId
            };

            await _seasons.AddAsync(entity, ct);
            await _seasons.SaveChangesAsync(ct);

            var created = await _seasons.GetByIdAsync(entity.Id, ct, false);
            if (created is null)
                throw new NotFoundException("Created season not found.");

            return SeasonMappings.FromEntity(created);
        }

        public async Task<SeasonDto?> UpdateAsync(int id, UpdateSeasonRequest req, CancellationToken ct)
        {
            var s = await _seasons.GetByIdAsync(id, ct, false);
            if (s is null) return null;

            if (req.Name is not null) s.Name = req.Name;
            if (req.StartDate.HasValue) s.StartDate = req.StartDate.Value;
            if (req.EndDate.HasValue) s.EndDate = req.EndDate.Value;
            if (req.StartDate.HasValue || req.EndDate.HasValue)
            {
                if (s.StartDate >= s.EndDate) throw new ArgumentException("StartDate must be before EndDate.");
            }

            if (req.MentorId != s.MentorId)
            {
                if (req.MentorId.HasValue)
                {
                    var mentor = await _users.GetByIdAsync(req.MentorId.Value, ct);
                    if (mentor is null) throw new InvalidOperationException("Mentor not found.");
                    if (mentor.KeycloakId == Guid.Empty) throw new InvalidOperationException("Mentor has no Keycloak link.");
                    var isMentor = await _kc.IsUserInGroupAsync(mentor.KeycloakId, "mentor", ct);
                    if (!isMentor) throw new InvalidOperationException("Selected user is not a mentor.");
                    s.MentorId = req.MentorId;
                }
                else
                {
                    s.MentorId = null;
                }
            }

            await _seasons.UpdateAsync(s, ct);
            await _seasons.SaveChangesAsync(ct);

            var updated = await _seasons.GetByIdAsync(s.Id, ct, false);
            return SeasonMappings.FromEntity(updated!);
        }

        public async Task<bool> DeleteAsync(int id, CancellationToken ct)
        {
            var s = await _seasons.GetByIdAsync(id, ct, true);
            if (s is null) return false;

            foreach (var u in s.Users.ToList())
            {
                u.SeasonId = null;
            }

            await _seasons.DeleteAsync(s, ct);
            await _seasons.SaveChangesAsync(ct);
            return true;
        }

        public async Task<AssignMentorResult> AssignMentorAsync(int id, Guid? mentorId, CancellationToken ct)
        {
            var s = await _seasons.GetByIdAsync(id, ct, false);
            if (s is null) return AssignMentorResult.NotFound;

            if (mentorId.HasValue)
            {
                var mentor = await _users.GetByIdAsync(mentorId.Value, ct);
                if (mentor is null) return AssignMentorResult.NotFound;
                if (mentor.KeycloakId == Guid.Empty) return AssignMentorResult.InvalidRole;
                var isMentor = await _kc.IsUserInGroupAsync(mentor.KeycloakId, "mentor", ct);
                if (!isMentor) return AssignMentorResult.InvalidRole;
                s.MentorId = mentorId;
            }
            else
            {
                s.MentorId = null;
            }

            await _seasons.UpdateAsync(s, ct);
            await _seasons.SaveChangesAsync(ct);
            return AssignMentorResult.Ok;
        }

        public async Task<AddUserResult> AddUserAsync(int id, Guid userId, CancellationToken ct)
        {
            var s = await _seasons.GetByIdAsync(id, ct, false);
            if (s is null) return AddUserResult.NotFound;

            var u = await _users.GetByIdAsync(userId, ct);
            if (u is null) return AddUserResult.NotFound;
            if (u.KeycloakId == Guid.Empty) return AddUserResult.InvalidRole;

            var isIntern = await _kc.IsUserInGroupAsync(u.KeycloakId, "intern", ct);
            if (!isIntern) return AddUserResult.InvalidRole;

            if (u.SeasonId.HasValue && u.SeasonId.Value != id) return AddUserResult.AlreadyInAnotherSeason;

            u.SeasonId = id;
            await _users.UpdateAsync(u, ct);
            await _users.SaveChangesAsync(ct);
            return AddUserResult.Ok;
        }

        public async Task<AddUserResult> AddUserByMentorAsync(int id, Guid userId, Guid mentorKeycloakId, CancellationToken ct)
        {
            var s = await _seasons.GetByIdAsync(id, ct, false);
            if (s is null) return AddUserResult.NotFound;

            var mentor = (await _users.GetAllAsync(ct)).FirstOrDefault(x => x.KeycloakId == mentorKeycloakId);
            if (mentor is null || s.MentorId != mentor.Id) return AddUserResult.NotFound;

            var u = await _users.GetByIdAsync(userId, ct);
            if (u is null) return AddUserResult.NotFound;
            if (u.KeycloakId == Guid.Empty) return AddUserResult.InvalidRole;

            var isIntern = await _kc.IsUserInGroupAsync(u.KeycloakId, "intern", ct);
            if (!isIntern)
            {
                var isGuest = await _kc.IsUserInGroupAsync(u.KeycloakId, "guest", ct);
                if (isGuest)
                    await _kc.ReplaceGroupsWithAsync(u.KeycloakId, "intern", ct);
                else
                    return AddUserResult.InvalidRole;
            }

            if (u.SeasonId.HasValue && u.SeasonId.Value != id) return AddUserResult.AlreadyInAnotherSeason;

            u.SeasonId = id;
            await _users.UpdateAsync(u, ct);
            await _users.SaveChangesAsync(ct);
            return AddUserResult.Ok;
        }

        public async Task<bool> RemoveUserAsync(int id, Guid userId, CancellationToken ct)
        {
            var s = await _seasons.GetByIdAsync(id, ct, false);
            if (s is null) return false;

            var u = await _users.GetByIdAsync(userId, ct);
            if (u is null) return false;
            if (u.SeasonId != id) return false;

            u.SeasonId = null;
            await _users.UpdateAsync(u, ct);
            await _users.SaveChangesAsync(ct);
            return true;
        }

        public async Task<bool> RemoveUserByMentorAsync(int id, Guid userId, Guid mentorKeycloakId, CancellationToken ct)
        {
            var s = await _seasons.GetByIdAsync(id, ct, false);
            if (s is null) return false;

            var mentor = (await _users.GetAllAsync(ct)).FirstOrDefault(x => x.KeycloakId == mentorKeycloakId);
            if (mentor is null || s.MentorId != mentor.Id) return false;

            var u = await _users.GetByIdAsync(userId, ct);
            if (u is null) return false;
            if (u.SeasonId != id) return false;

            u.SeasonId = null;
            await _users.UpdateAsync(u, ct);
            await _users.SaveChangesAsync(ct);

            if (u.KeycloakId != Guid.Empty)
                await _kc.ReplaceGroupsWithAsync(u.KeycloakId, "guest", ct);

            return true;
        }

        public async Task<IReadOnlyList<UserDto>> GetUsersAsync(int id, CancellationToken ct)
        {
            var s = await _seasons.GetByIdAsync(id, ct, true);
            if (s is null) return Array.Empty<UserDto>();
            return s.Users.Select(UserMappings.FromEntity).ToList();
        }

        public async Task<SeasonDto?> GetMySeasonAsync(Guid actorKeycloakId, CancellationToken ct)
        {
            var me = (await _users.GetAllAsync(ct)).FirstOrDefault(x => x.KeycloakId == actorKeycloakId);
            if (me is null || !me.SeasonId.HasValue) return null;
            var s = await _seasons.GetByIdAsync(me.SeasonId.Value, ct, false);
            return s is null ? null : SeasonMappings.FromEntity(s);
        }

        public async Task<IReadOnlyList<UserDto>> GetMySeasonUsersAsync(Guid actorKeycloakId, CancellationToken ct)
        {
            var me = (await _users.GetAllAsync(ct)).FirstOrDefault(x => x.KeycloakId == actorKeycloakId);
            if (me is null || !me.SeasonId.HasValue) return Array.Empty<UserDto>();
            var s = await _seasons.GetByIdAsync(me.SeasonId.Value, ct, true);
            if (s is null) return Array.Empty<UserDto>();
            return s.Users.Where(u => u.Id != me.Id).Select(UserMappings.FromEntity).ToList();
        }
    }
}
