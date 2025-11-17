using System;

namespace APLabApp.BLL.Feedbacks
{
    public record MentorInternAverageRowDto(
        Guid InternUserId,
        string FullName,
        string Email,
        double? AverageScore,
        int GradedFeedbacksCount
    );
}
