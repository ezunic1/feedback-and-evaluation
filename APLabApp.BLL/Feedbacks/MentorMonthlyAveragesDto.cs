using System;
using System.Collections.Generic;

namespace APLabApp.BLL.Feedbacks
{
    public record MentorMonthlyAveragesDto(
        int SeasonId,
        int MonthIndex,
        DateTime MonthStartUtc,
        DateTime MonthEndUtc,
        IReadOnlyList<MentorInternAverageDto> Items
    );
}
