using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace APLabApp.BLL.Feedbacks
{
    public interface IFeedbackService
    {
        Task<FeedbackDto> CreateInternFeedbackAsync(Guid senderUserId, CreateInternFeedbackRequest req, CancellationToken ct);
        Task<FeedbackDto> CreateMentorFeedbackAsync(Guid senderUserId, CreateMentorFeedbackRequest req, CancellationToken ct);
        Task DeleteAsync(int feedbackId, CancellationToken ct);

        Task<IReadOnlyList<FeedbackDto>> GetForInternAsync(Guid internUserId, int page, int pageSize, CancellationToken ct);
        Task<IReadOnlyList<FeedbackDto>> GetForMentorAsync(Guid mentorUserId, int page, int pageSize, CancellationToken ct);
        Task<IReadOnlyList<FeedbackDto>> GetForAdminAsync(int? seasonId, int page, int pageSize, CancellationToken ct);

        Task<IReadOnlyList<FeedbackDto>> GetReceivedFromMentorAsync(Guid internUserId, CancellationToken ct);
        Task<IReadOnlyList<FeedbackDto>> GetReceivedFromInternsAsync(Guid internUserId, CancellationToken ct);
        Task<IReadOnlyList<FeedbackDto>> GetSentByMeAsync(Guid userId, CancellationToken ct);

        Task<MentorMonthlyAveragesPageDto> GetMentorMonthlyAveragesPagedAsync(
            Guid mentorUserId,
            int seasonId,
            int monthIndex,
            string? sortBy,
            string? sortDir,
            int page,
            int pageSize,
            CancellationToken ct);
    }
}
