namespace APLabApp.BLL.Auth
{
    public sealed record RegisterRequest(string FullName, string Email, string Password);
}
