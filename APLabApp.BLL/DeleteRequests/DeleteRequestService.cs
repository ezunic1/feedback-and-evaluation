using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using APLabApp.BLL.DeleteRequests;
using APLabApp.BLL.Errors;
using APLabApp.Dal.Entities;
using APLabApp.Dal.Repositories;

namespace APLabApp.Bll.Services
{
    public class DeleteRequestService : IDeleteRequestService
    {
        private readonly IDeleteRequestRepository _deleteRequests;
        private readonly IFeedbackRepository _feedbacks;
        private readonly ISeasonRepository _seasons;

        public DeleteRequestService(
            IDeleteRequestRepository deleteRequests,
            IFeedbackRepository feedbacks,
            ISeasonRepository seasons)
        {
            _deleteRequests = deleteRequests;
            _feedbacks = feedbacks;
            _seasons = seasons;
        }

        public async Task<int> CreateAsync(CreateDeleteRequestDto dto, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(dto.Reason))
                throw new AppValidationException("Reason is required.");

            var feedback = await _feedbacks.GetByIdAsync(dto.FeedbackId, ct);
            if (feedback == null)
                throw new NotFoundException("Feedback not found.");

            var isParticipant =
                dto.SenderUserId == feedback.SenderUserId ||
                dto.SenderUserId == feedback.ReceiverUserId;

            var season = await _seasons.GetByIdAsync(feedback.SeasonId, ct);
            var isSeasonMentor = season != null && season.MentorId == dto.SenderUserId;

            if (!isParticipant && !isSeasonMentor)
                throw new ForbiddenException("Not allowed to request deletion for this feedback.");

            var dr = new DeleteRequest
            {
                FeedbackId = dto.FeedbackId,
                SenderUserId = dto.SenderUserId,
                Reason = dto.Reason.Trim()
            };

            await _deleteRequests.AddAsync(dr, ct);
            await _deleteRequests.SaveChangesAsync(ct);

            return dr.Id;
        }

        public async Task<List<DeleteRequestListItemDto>> GetAllAsync(CancellationToken ct)
        {
            var list = await _deleteRequests.GetAllAsync(ct);
            return list.Select(x => new DeleteRequestListItemDto
            {
                Id = x.Id,
                FeedbackId = x.FeedbackId,
                SenderUserId = x.SenderUserId,
                Reason = x.Reason,
                CreatedAtUtc = x.CreatedAtUtc
            }).ToList();
        }

        public async Task ApproveAsync(int deleteRequestId, CancellationToken ct)
        {
            var dr = await _deleteRequests.GetByIdAsync(deleteRequestId, ct);
            if (dr == null)
                throw new NotFoundException("Delete request not found.");

            var fb = await _feedbacks.GetByIdAsync(dr.FeedbackId, ct);
            if (fb != null)
                await _feedbacks.DeleteAsync(fb, ct);

            await _deleteRequests.DeleteAsync(dr, ct);
            await _deleteRequests.SaveChangesAsync(ct);
        }

        public async Task RejectAsync(int deleteRequestId, CancellationToken ct)
        {
            var dr = await _deleteRequests.GetByIdAsync(deleteRequestId, ct);
            if (dr == null)
                throw new NotFoundException("Delete request not found.");

            await _deleteRequests.DeleteAsync(dr, ct);
            await _deleteRequests.SaveChangesAsync(ct);
        }
    }
}
