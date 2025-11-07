using APLabApp.BLL.Users;

namespace APLabApp.BLL.Seasons
{
    public enum AssignMentorResult { Ok, NotFound, InvalidRole }
    public enum AddUserResult { Ok, NotFound, InvalidRole, AlreadyInAnotherSeason }

    public interface ISeasonService
    {
        Task<IReadOnlyList<SeasonDto>> GetAllAsync(CancellationToken ct);
        Task<SeasonDto?> GetByIdAsync(int id, bool includeUsers, CancellationToken ct);
        Task<SeasonDto> CreateAsync(CreateSeasonRequest req, CancellationToken ct);
        Task<SeasonDto?> UpdateAsync(int id, UpdateSeasonRequest req, CancellationToken ct);
        Task<bool> DeleteAsync(int id, CancellationToken ct);
        Task<AssignMentorResult> AssignMentorAsync(int id, Guid? mentorId, CancellationToken ct);
        Task<AddUserResult> AddUserAsync(int id, Guid userId, CancellationToken ct);
        Task<AddUserResult> AddUserByMentorAsync(int id, Guid userId, Guid mentorKeycloakId, CancellationToken ct);
        Task<bool> RemoveUserAsync(int id, Guid userId, CancellationToken ct);
        Task<bool> RemoveUserByMentorAsync(int id, Guid userId, Guid mentorKeycloakId, CancellationToken ct);
        Task<IReadOnlyList<UserDto>> GetUsersAsync(int id, CancellationToken ct);
        Task<SeasonDto?> GetMySeasonAsync(Guid actorKeycloakId, CancellationToken ct);
        Task<IReadOnlyList<UserDto>> GetMySeasonUsersAsync(Guid actorKeycloakId, CancellationToken ct);
    }
}
