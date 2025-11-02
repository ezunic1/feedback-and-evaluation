namespace APLabApp.BLL.Users;

public sealed record UpdateUserRequest(string? FullName, string? Desc, string? Email);
