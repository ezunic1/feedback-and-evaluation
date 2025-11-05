using APLabApp.BLL.Seasons;
using APLabApp.Dal.Entities;

namespace APLabApp.BLL.Seasons
{
    public interface ISeasonService
    {
        Task<IReadOnlyList<SeasonDto>> GetAllAsync(CancellationToken ct);
        Task<SeasonDto?> GetByIdAsync(int id, bool includeUsers, CancellationToken ct);
        Task<SeasonDto> CreateAsync(CreateSeasonRequest req, CancellationToken ct);
        Task<SeasonDto?> UpdateAsync(int id, UpdateSeasonRequest req, CancellationToken ct);
        Task<bool> DeleteAsync(int id, CancellationToken ct);
        Task<bool> AssignMentorAsync(int id, Guid? mentorId, CancellationToken ct);
        Task<bool> AddUserAsync(int id, Guid userId, CancellationToken ct);
        Task<bool> RemoveUserAsync(int id, Guid userId, CancellationToken ct);
        Task<IReadOnlyList<APLabApp.BLL.Users.UserDto>> GetUsersAsync(int id, CancellationToken ct);
    }
}
