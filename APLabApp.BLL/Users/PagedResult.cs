using System.Collections.Generic;

namespace APLabApp.BLL.Users
{
    public sealed record PagedResult<T>(
        IReadOnlyList<T> Items,
        int Page,
        int PageSize,
        int TotalCount,
        int TotalPages
    );
}
