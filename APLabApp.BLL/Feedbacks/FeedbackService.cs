using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using APLabApp.Dal.Entities;
using APLabApp.Dal.Repositories;
using Microsoft.EntityFrameworkCore;

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
            if (string.IsNullOrWhiteSpace(req.Comment))
                throw new ArgumentException("Comment is required.", nameof(req.Comment));

            var sender = await _users.GetByIdAsync(senderUserId, ct);
            if (sender is null)
                throw new InvalidOperationException("Sender not found.");

            if (!sender.SeasonId.HasValue)
                throw new InvalidOperationException("Intern must be assigned to a season to send feedback.");

            var season = await _seasons.GetByIdAsync(sender.SeasonId.Value, ct);
            if (season is null)
                throw new InvalidOperationException("Season not found.");

            var now = DateTime.UtcNow;
            if (now < season.StartDate || now > season.EndDate)
                throw new InvalidOperationException("Feedback can only be sent during an active season.");

            var receiver = await _users.GetByIdAsync(req.ReceiverUserId, ct);
            if (receiver is null)
                throw new InvalidOperationException("Receiver not found.");

            if (receiver.Id == sender.Id)
                throw new InvalidOperationException("Cannot send feedback to yourself.");

            var receiverIsInternInSameSeason = receiver.SeasonId == season.Id;
            var receiverIsMentorOfSeason = season.MentorId == receiver.Id;

            if (!receiverIsInternInSameSeason && !receiverIsMentorOfSeason)
                throw new InvalidOperationException("Intern can send feedback only to interns in the same season or the mentor of that season.");

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
            if (string.IsNullOrWhiteSpace(req.Comment))
                throw new ArgumentException("Comment is required.", nameof(req.Comment));

            ValidateScore(req.CareerSkills, nameof(req.CareerSkills));
            ValidateScore(req.Communication, nameof(req.Communication));
            ValidateScore(req.Collaboration, nameof(req.Collaboration));

            var sender = await _users.GetByIdAsync(senderUserId, ct);
            if (sender is null)
                throw new InvalidOperationException("Sender not found.");

            var receiver = await _users.GetByIdAsync(req.ReceiverUserId, ct);
            if (receiver is null)
                throw new InvalidOperationException("Receiver not found.");

            if (!receiver.SeasonId.HasValue)
                throw new InvalidOperationException("Mentor can only send graded feedback to an intern assigned to a season.");

            var season = await _seasons.GetByIdAsync(receiver.SeasonId.Value, ct);
            if (season is null)
                throw new InvalidOperationException("Season not found.");

            if (season.MentorId != sender.Id)
                throw new InvalidOperationException("Mentor can only send feedback to interns in their own season.");

            var now = DateTime.UtcNow;
            if (now < season.StartDate || now > season.EndDate)
                throw new InvalidOperationException("Feedback can only be sent during an active season.");

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
                return;

            await _feedbacks.DeleteAsync(feedback, ct);
            await _feedbacks.SaveChangesAsync(ct);
        }

        public async Task<IReadOnlyList<FeedbackDto>> GetForInternAsync(Guid internUserId, int page, int pageSize, CancellationToken ct)
        {
            var user = await _users.GetByIdAsync(internUserId, ct);
            if (user is null)
                throw new InvalidOperationException("User not found.");

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
                throw new InvalidOperationException("User not found.");

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

        private static void ValidateScore(int score, string paramName)
        {
            if (score < 1 || score > 5)
                throw new ArgumentOutOfRangeException(paramName, "Score must be between 1 and 5.");
        }
    }
}
