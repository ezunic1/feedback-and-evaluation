using System;

namespace APLabApp.BLL.Users
{
    public sealed record UsersQuery(
        int Page = 1,
        int PageSize = 20,
        string? Q = null,
        string? Role = null,
        DateTime? From = null,
        DateTime? To = null,
        string SortBy = "createdAt",
        string SortDir = "desc",
        int? SeasonId = null
    );
}
