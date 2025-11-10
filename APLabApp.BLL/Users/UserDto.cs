using System;

namespace APLabApp.BLL.Users
{
    public sealed record UserDto(
        Guid Id,
        Guid KeycloakId,
        string FullName,
        string? Desc,
        string Email,
        int? SeasonId,
        DateTime CreatedAtUtc
    )
    {
        public string? RoleName { get; init; }
        public string? SeasonName { get; init; }
    }
}
