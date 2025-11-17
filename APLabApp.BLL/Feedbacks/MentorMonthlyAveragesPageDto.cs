using System;
using System.Collections.Generic;

namespace APLabApp.BLL.Feedbacks
{
    public record MentorMonthlyAveragesPageDto(
        int SeasonId,
        int MonthIndex,
        DateTime MonthStartUtc,
        DateTime MonthEndUtc,
        int Page,
        int PageSize,
        int Total,
        int TotalPages,
        IReadOnlyList<MentorInternAverageRowDto> Items
    );
}
