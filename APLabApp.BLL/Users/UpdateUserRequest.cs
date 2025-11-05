namespace APLabApp.BLL.Users
{
    public sealed record UpdateUserRequest(string? FullName, string? Email, string? Desc, int? SeasonId);
}
