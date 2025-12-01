using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using APLabApp.BLL.Errors;
using APLabApp.Dal.Entities;
using APLabApp.Dal.Repositories;
using Microsoft.EntityFrameworkCore;

using DomainValidationException = APLabApp.BLL.Errors.AppValidationException;

namespace APLabApp.BLL.Feedbacks
{
    public class FeedbackService : IFeedbackService
    {
        private readonly IUserRepository _users;
        private readonly ISeasonRepository _seasons;
        private readonly IFeedbackRepository _feedbacks;
        private readonly IGradeRepository _grades;

        public FeedbackService(
            IUserRepository users,
            ISeasonRepository seasons,
            IFeedbackRepository feedbacks,
            IGradeRepository grades)
        {
            _users = users;
            _seasons = seasons;
            _feedbacks = feedbacks;
            _grades = grades;
        }

        public async Task<FeedbackDto> CreateInternFeedbackAsync(Guid senderUserId, CreateInternFeedbackRequest req, CancellationToken ct)
        {
            var sender = await _users.GetByIdAsync(senderUserId, ct);
            if (sender is null)
                throw new NotFoundException("Sender not found.");

            if (!sender.SeasonId.HasValue)
                throw new ForbiddenException("Intern must be assigned to a season to send feedback.");

            var season = await _seasons.GetByIdAsync(sender.SeasonId.Value, ct);
            if (season is null)
                throw new NotFoundException("Season not found.");

            var now = DateTime.UtcNow;
            if (now < season.StartDate || now > season.EndDate)
                throw new DomainValidationException("Feedback can only be sent during an active season.");

            var receiver = await _users.GetByIdAsync(req.ReceiverUserId, ct);
            if (receiver is null)
                throw new NotFoundException("Receiver not found.");

            if (receiver.Id == sender.Id)
                throw new DomainValidationException("Cannot send feedback to yourself.");

            var receiverIsInternInSameSeason = receiver.SeasonId == season.Id;
            var receiverIsMentorOfSeason = season.MentorId == receiver.Id;

            if (!receiverIsInternInSameSeason && !receiverIsMentorOfSeason)
                throw new ForbiddenException("Intern can send feedback only to interns in the same season or the mentor of that season.");

            var feedback = new Feedback
            {
                SeasonId = season.Id,
                SenderUserId = sender.Id,
                ReceiverUserId = receiver.Id,
                Comment = req.Comment.Trim(),
                CreatedAtUtc = DateTime.UtcNow
            };

            await _feedbacks.AddAsync(feedback, ct);
            await _feedbacks.SaveChangesAsync(ct);

            return new FeedbackDto(
                feedback.Id,
                feedback.SeasonId,
                feedback.SenderUserId,
                feedback.ReceiverUserId,
                feedback.Comment,
                feedback.CreatedAtUtc,
                null
            );
        }

        public async Task<FeedbackDto> CreateMentorFeedbackAsync(Guid senderUserId, CreateMentorFeedbackRequest req, CancellationToken ct)
        {
            var sender = await _users.GetByIdAsync(senderUserId, ct);
            if (sender is null)
                throw new NotFoundException("Sender not found.");

            var receiver = await _users.GetByIdAsync(req.ReceiverUserId, ct);
            if (receiver is null)
                throw new NotFoundException("Receiver not found.");

            if (!receiver.SeasonId.HasValue)
                throw new ForbiddenException("Mentor can only send graded feedback to an intern assigned to a season.");

            var season = await _seasons.GetByIdAsync(receiver.SeasonId.Value, ct);
            if (season is null)
                throw new NotFoundException("Season not found.");

            if (season.MentorId != sender.Id)
                throw new ForbiddenException("Mentor can only send feedback to interns in their own season.");

            var now = DateTime.UtcNow;
            if (now < season.StartDate || now > season.EndDate)
                throw new DomainValidationException("Feedback can only be sent during an active season.");

            var feedback = new Feedback
            {
                SeasonId = season.Id,
                SenderUserId = sender.Id,
                ReceiverUserId = receiver.Id,
                Comment = req.Comment.Trim(),
                CreatedAtUtc = DateTime.UtcNow
            };

            var grade = new Grade
            {
                Feedback = feedback,
                CareerSkills = req.CareerSkills,
                Communication = req.Communication,
                Collaboration = req.Collaboration
            };

            await _feedbacks.AddAsync(feedback, ct);
            await _grades.AddAsync(grade, ct);
            await _feedbacks.SaveChangesAsync(ct);

            var gradeDto = new GradeDto(
                grade.CareerSkills,
                grade.Communication,
                grade.Collaboration
            );

            return new FeedbackDto(
                feedback.Id,
                feedback.SeasonId,
                feedback.SenderUserId,
                feedback.ReceiverUserId,
                feedback.Comment,
                feedback.CreatedAtUtc,
                gradeDto
            );
        }

        public async Task DeleteAsync(int feedbackId, CancellationToken ct)
        {
            var feedback = await _feedbacks.GetByIdAsync(feedbackId, ct);
            if (feedback is null)
                throw new NotFoundException("Feedback not found.");

            await _feedbacks.DeleteAsync(feedback, ct);
            await _feedbacks.SaveChangesAsync(ct);
        }

        public async Task<IReadOnlyList<FeedbackDto>> GetForInternAsync(Guid internUserId, int page, int pageSize, CancellationToken ct)
        {
            var user = await _users.GetByIdAsync(internUserId, ct);
            if (user is null)
                throw new NotFoundException("User not found.");

            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            var skip = (page - 1) * pageSize;

            var query = _feedbacks.Query()
                .AsNoTracking()
                .Include(f => f.Grade)
                .Where(f => f.SenderUserId == user.Id || f.ReceiverUserId == user.Id);

            var list = await query
                .OrderByDescending(f => f.CreatedAtUtc)
                .Skip(skip)
                .Take(pageSize)
                .ToListAsync(ct);

            return list
                .Select(f => new FeedbackDto(
                    f.Id,
                    f.SeasonId,
                    f.SenderUserId,
                    f.ReceiverUserId,
                    f.Comment,
                    f.CreatedAtUtc,
                    f.Grade is null
                        ? null
                        : new GradeDto(
                            f.Grade.CareerSkills,
                            f.Grade.Communication,
                            f.Grade.Collaboration)))
                .ToList();
        }

        public async Task<IReadOnlyList<FeedbackDto>> GetForMentorAsync(Guid mentorUserId, int page, int pageSize, CancellationToken ct)
        {
            var mentor = await _users.GetByIdAsync(mentorUserId, ct);
            if (mentor is null)
                throw new NotFoundException("User not found.");

            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            var skip = (page - 1) * pageSize;

            var query = _feedbacks.Query()
                .AsNoTracking()
                .Include(f => f.Grade)
                .Include(f => f.Season)
                .Where(f => f.Season.MentorId == mentor.Id);

            var list = await query
                .OrderByDescending(f => f.CreatedAtUtc)
                .Skip(skip)
                .Take(pageSize)
                .ToListAsync(ct);

            return list
                .Select(f => new FeedbackDto(
                    f.Id,
                    f.SeasonId,
                    f.SenderUserId,
                    f.ReceiverUserId,
                    f.Comment,
                    f.CreatedAtUtc,
                    f.Grade is null
                        ? null
                        : new GradeDto(
                            f.Grade.CareerSkills,
                            f.Grade.Communication,
                            f.Grade.Collaboration)))
                .ToList();
        }

        public async Task<IReadOnlyList<FeedbackDto>> GetForAdminAsync(int? seasonId, int page, int pageSize, CancellationToken ct)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            var skip = (page - 1) * pageSize;

            var query = _feedbacks.Query()
                .AsNoTracking()
                .Include(f => f.Grade)
                .AsQueryable();

            if (seasonId.HasValue)
                query = query.Where(f => f.SeasonId == seasonId.Value);

            var list = await query
                .OrderByDescending(f => f.CreatedAtUtc)
                .Skip(skip)
                .Take(pageSize)
                .ToListAsync(ct);

            return list
                .Select(f => new FeedbackDto(
                    f.Id,
                    f.SeasonId,
                    f.SenderUserId,
                    f.ReceiverUserId,
                    f.Comment,
                    f.CreatedAtUtc,
                    f.Grade is null
                        ? null
                        : new GradeDto(
                            f.Grade.CareerSkills,
                            f.Grade.Communication,
                            f.Grade.Collaboration)))
                .ToList();
        }

        public async Task<IReadOnlyList<FeedbackDto>> GetReceivedFromMentorAsync(Guid internUserId, CancellationToken ct)
        {
            var list = await _feedbacks.Query()
                .AsNoTracking()
                .Include(f => f.Grade)
                .Include(f => f.Season)
                .Where(f => f.ReceiverUserId == internUserId && f.Grade != null)
                .OrderByDescending(f => f.CreatedAtUtc)
                .ToListAsync(ct);

            return list.Select(f => new FeedbackDto(
                f.Id,
                f.SeasonId,
                f.SenderUserId,
                f.ReceiverUserId,
                f.Comment,
                f.CreatedAtUtc,
                f.Grade == null ? null : new GradeDto(f.Grade.CareerSkills, f.Grade.Communication, f.Grade.Collaboration)
            )).ToList();
        }

        public async Task<IReadOnlyList<FeedbackDto>> GetReceivedFromInternsAsync(Guid internUserId, CancellationToken ct)
        {
            var list = await _feedbacks.Query()
                .AsNoTracking()
                .Include(f => f.Grade)
                .Where(f => f.ReceiverUserId == internUserId && f.Grade == null)
                .OrderByDescending(f => f.CreatedAtUtc)
                .ToListAsync(ct);

            return list.Select(f => new FeedbackDto(
                f.Id,
                f.SeasonId,
                f.SenderUserId,
                f.ReceiverUserId,
                f.Comment,
                f.CreatedAtUtc,
                null
            )).ToList();
        }

        public async Task<IReadOnlyList<FeedbackDto>> GetSentByMeAsync(Guid userId, CancellationToken ct)
        {
            var list = await _feedbacks.Query()
                .AsNoTracking()
                .Include(f => f.Grade)
                .Where(f => f.SenderUserId == userId)
                .OrderByDescending(f => f.CreatedAtUtc)
                .ToListAsync(ct);

            return list.Select(f => new FeedbackDto(
                f.Id,
                f.SeasonId,
                f.SenderUserId,
                f.ReceiverUserId,
                f.Comment,
                f.CreatedAtUtc,
                f.Grade == null ? null : new GradeDto(f.Grade.CareerSkills, f.Grade.Communication, f.Grade.Collaboration)
            )).ToList();
        }

        public async Task<MentorMonthlyAveragesPageDto> GetMentorMonthlyAveragesPagedAsync(Guid mentorUserId, int seasonId, int monthIndex, string? sortBy, string? sortDir, int page, int pageSize, CancellationToken ct)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;

            var mentor = await _users.GetByIdAsync(mentorUserId, ct);
            if (mentor is null)
                throw new NotFoundException("User not found.");

            var season = await _seasons.GetByIdAsync(seasonId, ct);
            if (season is null)
                throw new NotFoundException("Season not found.");

            if (season.MentorId != mentor.Id)
                throw new ForbiddenException("Forbidden for this season.");

            var now = DateTime.UtcNow;
            var seasonStart = new DateTime(season.StartDate.Year, season.StartDate.Month, season.StartDate.Day, 0, 0, 0, DateTimeKind.Utc);
            var rawEnd = season.EndDate;
            var seasonEndClamped = rawEnd < now ? rawEnd : now;

            if (seasonEndClamped <= seasonStart)
            {
                var empty = new List<MentorInternAverageRowDto>();
                return new MentorMonthlyAveragesPageDto(season.Id, 1, seasonStart, seasonEndClamped, 1, pageSize, 0, 0, empty);
            }

            var spans = new List<(DateTime Start, DateTime End)>();
            var cur = seasonStart;
            while (cur < seasonEndClamped)
            {
                var next = cur.AddMonths(1);
                var spanEnd = next < seasonEndClamped ? next : seasonEndClamped;
                spans.Add((cur, spanEnd));
                cur = next;
            }

            if (spans.Count == 0)
            {
                spans.Add((seasonStart, seasonEndClamped));
            }

            if (monthIndex < 1) monthIndex = 1;
            if (monthIndex > spans.Count) monthIndex = spans.Count;
            var slot = spans[monthIndex - 1];
            var slotStart = slot.Start;
            var slotEnd = slot.End;

            var interns = await _users.Query()
                .AsNoTracking()
                .Where(u => u.SeasonId == season.Id && u.Id != season.MentorId)
                .Select(u => new { u.Id, u.FullName, u.Email })
                .ToListAsync(ct);

            var gradedAgg = await _feedbacks.Query()
                .AsNoTracking()
                .Include(f => f.Grade)
                .Where(f =>
                    f.SeasonId == season.Id &&
                    f.SenderUserId == mentor.Id &&
                    f.Grade != null &&
                    f.CreatedAtUtc >= slotStart &&
                    f.CreatedAtUtc < slotEnd)
                .GroupBy(f => f.ReceiverUserId)
                .Select(g => new
                {
                    InternUserId = g.Key,
                    AverageScore = g.Average(f => (f.Grade!.CareerSkills + f.Grade!.Communication + f.Grade!.Collaboration) / 3.0),
                    GradedFeedbacksCount = g.Count()
                })
                .ToListAsync(ct);

            var aggMap = gradedAgg.ToDictionary(x => x.InternUserId, x => new { x.AverageScore, x.GradedFeedbacksCount });

            var rows = interns
                .Select(i =>
                {
                    var has = aggMap.TryGetValue(i.Id, out var a);
                    var avg = has ? a!.AverageScore : (double?)null;
                    var cnt = has ? a!.GradedFeedbacksCount : 0;
                    return new MentorInternAverageRowDto(i.Id, i.FullName ?? string.Empty, i.Email ?? string.Empty, avg, cnt);
                })
                .ToList();

            var sb = (sortBy ?? "grade").Trim().ToLowerInvariant();
            var sd = (sortDir ?? "desc").Trim().ToLowerInvariant();

            if (sb == "name")
            {
                if (sd == "asc")
                    rows = rows.OrderBy(r => r.FullName).ThenBy(r => r.Email).ToList();
                else
                    rows = rows.OrderByDescending(r => r.FullName).ThenByDescending(r => r.Email).ToList();
            }
            else
            {
                if (sd == "asc")
                    rows = rows.OrderBy(r => r.AverageScore.HasValue ? 0 : 1).ThenBy(r => r.AverageScore).ToList();
                else
                    rows = rows.OrderBy(r => r.AverageScore.HasValue ? 0 : 1).ThenByDescending(r => r.AverageScore).ToList();
            }

            var total = rows.Count;
            var totalPages = total == 0 ? 0 : (int)Math.Ceiling(total / (double)pageSize);
            if (totalPages == 0) page = 1;
            else if (page > totalPages) page = totalPages;

            var skip = (page - 1) * pageSize;
            var pageItems = rows.Skip(skip).Take(pageSize).ToList();

            return new MentorMonthlyAveragesPageDto(season.Id, monthIndex, slotStart, slotEnd, page, pageSize, total, totalPages, pageItems);
        }

        public async Task<APLabApp.BLL.PagedResult<FeedbackDto>> SearchForAdminAsync(int seasonId, string? type, string? sortDir, int? monthIndex, int page, int pageSize, CancellationToken ct)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;

            var q = _feedbacks.Query()
                .AsNoTracking()
                .Include(f => f.Grade)
                .Include(f => f.Season)
                .Where(f => f.SeasonId == seasonId);

            q = ApplyTypeFilter(q, type);

            if (monthIndex.HasValue && monthIndex.Value > 0)
            {
                var spans = await GetSeasonMonthSpansAsync(seasonId, ct);
                if (spans.Count > 0)
                {
                    var idx = monthIndex.Value > spans.Count ? spans.Count : monthIndex.Value;
                    var span = spans[idx - 1];
                    q = q.Where(f => f.CreatedAtUtc >= span.StartUtc && f.CreatedAtUtc < span.EndUtc);
                }
            }

            var sd = (sortDir ?? "desc").Trim().ToLowerInvariant();
            q = sd == "asc"
                ? q.OrderBy(f => f.CreatedAtUtc).ThenBy(f => f.Id)
                : q.OrderByDescending(f => f.CreatedAtUtc).ThenByDescending(f => f.Id);

            var total = await q.CountAsync(ct);
            var totalPages = total == 0 ? 0 : (int)Math.Ceiling(total / (double)pageSize);
            if (totalPages == 0) page = 1;
            else if (page > totalPages) page = totalPages;

            var skip = (page - 1) * pageSize;

            var items = await q.Skip(skip).Take(pageSize)
                .Select(f => new FeedbackDto(
                    f.Id,
                    f.SeasonId,
                    f.SenderUserId,
                    f.ReceiverUserId,
                    f.Comment,
                    f.CreatedAtUtc,
                    f.Grade == null ? null : new GradeDto(f.Grade.CareerSkills, f.Grade.Communication, f.Grade.Collaboration)))
                .ToListAsync(ct);

            return new APLabApp.BLL.PagedResult<FeedbackDto>(items, page, pageSize, total, totalPages);
        }

        public async Task<APLabApp.BLL.PagedResult<FeedbackDto>> SearchForMentorAsync(Guid mentorUserId, int seasonId, string? type, string? sortDir, int? monthIndex, int page, int pageSize, CancellationToken ct)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;

            var season = await _seasons.GetByIdAsync(seasonId, ct);
            if (season is null)
                throw new NotFoundException("Season not found.");
            if (season.MentorId != mentorUserId)
                throw new ForbiddenException("Forbidden for this season.");

            var q = _feedbacks.Query()
                .AsNoTracking()
                .Include(f => f.Grade)
                .Include(f => f.Season)
                .Where(f => f.SeasonId == seasonId && f.Season.MentorId == mentorUserId);

            q = ApplyTypeFilter(q, type);

            if (monthIndex.HasValue && monthIndex.Value > 0)
            {
                var spans = await GetSeasonMonthSpansAsync(seasonId, ct);
                if (spans.Count > 0)
                {
                    var idx = monthIndex.Value > spans.Count ? spans.Count : monthIndex.Value;
                    var span = spans[idx - 1];
                    q = q.Where(f => f.CreatedAtUtc >= span.StartUtc && f.CreatedAtUtc < span.EndUtc);
                }
            }

            var sd = (sortDir ?? "desc").Trim().ToLowerInvariant();
            q = sd == "asc"
                ? q.OrderBy(f => f.CreatedAtUtc).ThenBy(f => f.Id)
                : q.OrderByDescending(f => f.CreatedAtUtc).ThenByDescending(f => f.Id);

            var total = await q.CountAsync(ct);
            var totalPages = total == 0 ? 0 : (int)Math.Ceiling(total / (double)pageSize);
            if (totalPages == 0) page = 1;
            else if (page > totalPages) page = totalPages;

            var skip = (page - 1) * pageSize;

            var items = await q.Skip(skip).Take(pageSize)
                .Select(f => new FeedbackDto(
                    f.Id,
                    f.SeasonId,
                    f.SenderUserId,
                    f.ReceiverUserId,
                    f.Comment,
                    f.CreatedAtUtc,
                    f.Grade == null ? null : new GradeDto(f.Grade.CareerSkills, f.Grade.Communication, f.Grade.Collaboration)))
                .ToListAsync(ct);

            return new APLabApp.BLL.PagedResult<FeedbackDto>(items, page, pageSize, total, totalPages);
        }

        public async Task<IReadOnlyList<MonthSpanDto>> GetSeasonMonthSpansAsync(int seasonId, CancellationToken ct)
        {
            var season = await _seasons.GetByIdAsync(seasonId, ct);
            if (season is null)
                throw new NotFoundException("Season not found.");

            var now = DateTime.UtcNow;
            var start = new DateTime(season.StartDate.Year, season.StartDate.Month, season.StartDate.Day, 0, 0, 0, DateTimeKind.Utc);
            var end = season.EndDate < now ? season.EndDate : now;

            var result = new List<MonthSpanDto>();
            if (end <= start)
                return result;

            var cur = start;
            var idx = 1;
            while (cur < end)
            {
                var next = cur.AddMonths(1);
                var spanEnd = next < end ? next : end;
                result.Add(new MonthSpanDto(idx++, cur, spanEnd));
                cur = next;
            }

            if (result.Count == 0)
                result.Add(new MonthSpanDto(1, start, end));

            return result;
        }

        private static IQueryable<Feedback> ApplyTypeFilter(IQueryable<Feedback> q, string? type)
        {
            var t = (type ?? "all").Trim().ToLowerInvariant();
            if (t == "i2i")
                q = q.Where(f => f.Grade == null && f.SenderUserId != f.Season.MentorId && f.ReceiverUserId != f.Season.MentorId);
            else if (t == "i2m")
                q = q.Where(f => f.Grade == null && f.ReceiverUserId == f.Season.MentorId);
            else if (t == "m2i")
                q = q.Where(f => f.Grade != null && f.SenderUserId == f.Season.MentorId);
            return q;
        }
    }
}
