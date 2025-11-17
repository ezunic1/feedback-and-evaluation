using System;

namespace APLabApp.BLL.Feedbacks
{
    public record MonthlyAverageDto(
        int SeasonId,
        int MonthIndex,
        DateTime MonthStartUtc,
        DateTime MonthEndUtc,
        double? AverageScore,
        int GradedFeedbacksCount
    );
}
