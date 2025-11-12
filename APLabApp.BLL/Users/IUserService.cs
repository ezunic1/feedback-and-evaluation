using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace APLabApp.BLL.Users
{
    public interface IUserService
    {
        Task<PagedResult<UserListItemDto>> GetPagedAsync(UsersQuery q, CancellationToken ct);
        Task<IReadOnlyList<UserDto>> GetAllAsync(CancellationToken ct);
        Task<UserDto?> GetByIdAsync(Guid id, CancellationToken ct);
        Task<UserDto> CreateAsync(CreateUserRequest req, CancellationToken ct);
        Task<UserDto?> UpdateAsync(Guid id, UpdateUserRequest req, CancellationToken ct);
        Task<bool> DeleteAsync(Guid id, CancellationToken ct);
        Task<bool> ChangePasswordAsync(Guid id, string newPassword, string? currentPassword, CancellationToken ct);
        Task<UserDto> CreateGuestAsync(CreateUserRequest req, string password, CancellationToken ct);
        Task<UserDto?> GetByKeycloakIdAsync(Guid keycloakId, CancellationToken ct);
        Task<UserDto> EnsureLocalUserAsync(Guid keycloakId, string? email, string? fullName, CancellationToken ct);
        Task<UserDto?> UpdateSelfAsync(Guid keycloakId, string fullName, string? description, CancellationToken ct);
    }
}
