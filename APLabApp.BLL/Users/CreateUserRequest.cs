namespace APLabApp.BLL.Users
{
    public sealed record CreateUserRequest(string FullName, string Email, string? Desc, int? SeasonId);
}
