using System;
using APLabApp.Dal.Entities;

namespace APLabApp.BLL.Users
{
    public static class UserMappings
    {
        public static UserDto FromEntity(User e) =>
            new UserDto(e.Id, e.KeycloakId, e.FullName, e.Desc, e.Email, e.SeasonId, e.CreatedAtUtc);

        public static User ToEntity(this CreateUserRequest req) =>
            new User
            {
                Id = Guid.NewGuid(),
                KeycloakId = Guid.Empty,
                FullName = req.FullName,
                Email = req.Email,
                Desc = req.Desc,
                SeasonId = req.SeasonId,
                CreatedAtUtc = DateTime.UtcNow
            };

        public static void Apply(this UpdateUserRequest req, User e)
        {
            if (!string.IsNullOrWhiteSpace(req.FullName)) e.FullName = req.FullName!;
            if (!string.IsNullOrWhiteSpace(req.Email)) e.Email = req.Email!;
            if (req.Desc is not null) e.Desc = req.Desc;
            if (req.SeasonId is not null) e.SeasonId = req.SeasonId;
        }
    }
}
