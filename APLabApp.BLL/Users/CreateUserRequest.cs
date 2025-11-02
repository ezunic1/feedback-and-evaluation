namespace APLabApp.BLL.Users;

public sealed record CreateUserRequest(Guid KeycloakId, string FullName, string? Desc, string? Email);
