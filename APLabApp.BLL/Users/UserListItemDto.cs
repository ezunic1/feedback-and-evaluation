using System;

namespace APLabApp.BLL.Users
{
    public sealed record UserListItemDto(
        Guid Id,
        string FullName,
        string Email,
        string Role,
        DateTime CreatedAt
    );
}
