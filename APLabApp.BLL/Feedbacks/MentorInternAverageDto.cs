using System;

namespace APLabApp.BLL.Feedbacks
{
    public record MentorInternAverageDto(
        Guid InternUserId,
        double? AverageScore,
        int GradedFeedbacksCount
    );
}
