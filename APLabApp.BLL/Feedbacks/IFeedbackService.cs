using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace APLabApp.BLL.Feedbacks
{
    public sealed class MonthSpanDto
    {
        public int Index { get; }
        public DateTime StartUtc { get; }
        public DateTime EndUtc { get; }

        public MonthSpanDto(int index, DateTime startUtc, DateTime endUtc)
        {
            Index = index;
            StartUtc = startUtc;
            EndUtc = endUtc;
        }
    }

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

        Task<MentorMonthlyAveragesPageDto> GetMentorMonthlyAveragesPagedAsync(Guid mentorUserId, int seasonId, int monthIndex, string? sortBy, string? sortDir, int page, int pageSize, CancellationToken ct);

        Task<APLabApp.BLL.PagedResult<FeedbackDto>> SearchForAdminAsync(int seasonId, string? type, string? sortDir, int? monthIndex, int page, int pageSize, CancellationToken ct);
        Task<APLabApp.BLL.PagedResult<FeedbackDto>> SearchForMentorAsync(Guid mentorUserId, int seasonId, string? type, string? sortDir, int? monthIndex, int page, int pageSize, CancellationToken ct);

        Task<IReadOnlyList<MonthSpanDto>> GetSeasonMonthSpansAsync(int seasonId, CancellationToken ct);
    }
}
