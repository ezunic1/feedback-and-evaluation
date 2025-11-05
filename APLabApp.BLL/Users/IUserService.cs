using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace APLabApp.BLL.Users
{
    public interface IUserService
    {
        Task<IReadOnlyList<UserDto>> GetAllAsync(CancellationToken ct);
        Task<UserDto?> GetByIdAsync(Guid id, CancellationToken ct);
        Task<UserDto> CreateAsync(CreateUserRequest req, CancellationToken ct);
        Task<UserDto?> UpdateAsync(Guid id, UpdateUserRequest req, CancellationToken ct);
        Task<bool> DeleteAsync(Guid id, CancellationToken ct);
        Task<bool> ChangePasswordAsync(Guid id, string newPassword, string? currentPassword, CancellationToken ct);
        Task<UserDto> CreateGuestAsync(CreateUserRequest req, string password, CancellationToken ct);
    }
}
