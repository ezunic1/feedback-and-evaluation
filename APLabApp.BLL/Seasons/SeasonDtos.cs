using System;

namespace APLabApp.BLL.Seasons
{
    public sealed record SeasonDto(
        int Id,
        string Name,
        DateTime StartDate,
        DateTime EndDate,
        Guid? MentorId,
        string? MentorName,
        int UsersCount
    );

    public sealed record CreateSeasonRequest(
        string Name,
        DateTime StartDate,
        DateTime EndDate,
        Guid? MentorId
    );

    public sealed record UpdateSeasonRequest(
        string? Name,
        DateTime? StartDate,
        DateTime? EndDate,
        Guid? MentorId
    );

    public static class SeasonMappings
    {
        public static SeasonDto FromEntity(APLabApp.Dal.Entities.Season s) =>
            new SeasonDto(
                s.Id,
                s.Name,
                s.StartDate,
                s.EndDate,
                s.MentorId,
                s.Mentor?.FullName,
                s.Users?.Count ?? 0
            );
    }
}
