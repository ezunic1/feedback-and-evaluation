using APLabApp.Dal.Entities;

namespace APLabApp.BLL.Users;

public static class UserMappings
{
    public static UserDto FromEntity(User e) =>
        new UserDto(e.Id, e.KeycloakId, e.FullName, e.Desc, e.Email, e.CreatedAtUtc);

    public static User ToEntity(this CreateUserRequest req) =>
        new User
        {
            Id = Guid.NewGuid(),
            KeycloakId = req.KeycloakId,
            FullName = req.FullName,
            Desc = req.Desc,
            Email = req.Email,
            CreatedAtUtc = DateTime.UtcNow
        };

    public static void Apply(this UpdateUserRequest req, User e)
    {
        if (req.FullName is not null) e.FullName = req.FullName;
        if (req.Desc is not null) e.Desc = req.Desc;
        if (req.Email is not null) e.Email = req.Email;
    }
}
