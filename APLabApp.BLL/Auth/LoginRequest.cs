namespace APLabApp.BLL.Auth
{
    public sealed record LoginRequest(string UsernameOrEmail, string Password, string? RedirectUri);
}
