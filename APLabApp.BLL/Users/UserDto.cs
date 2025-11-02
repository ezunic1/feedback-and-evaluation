namespace APLabApp.BLL.Users;

public sealed record UserDto(
    Guid Id, Guid KeycloakId, string FullName, string? Desc, string? Email, DateTime CreatedAtUtc
);
