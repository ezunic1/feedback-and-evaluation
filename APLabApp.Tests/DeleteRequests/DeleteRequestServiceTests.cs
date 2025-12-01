using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using APLabApp.Bll.Services;
using APLabApp.BLL.DeleteRequests;
using APLabApp.BLL.Errors;
using APLabApp.Dal.Entities;
using APLabApp.Dal.Repositories;
using Moq;
using Xunit;

namespace APLabApp.Tests.DeleteRequests
{
    public class DeleteRequestServiceTests
    {
        private readonly Mock<IDeleteRequestRepository> _deleteRequests;
        private readonly Mock<IFeedbackRepository> _feedbacks;
        private readonly Mock<ISeasonRepository> _seasons;
        private readonly DeleteRequestService _sut;

        public DeleteRequestServiceTests()
        {
            _deleteRequests = new Mock<IDeleteRequestRepository>();
            _feedbacks = new Mock<IFeedbackRepository>();
            _seasons = new Mock<ISeasonRepository>();

            _sut = new DeleteRequestService(
                _deleteRequests.Object,
                _feedbacks.Object,
                _seasons.Object);
        }

        private static Feedback CreateFeedback(
            int id,
            int seasonId,
            Guid senderId,
            Guid receiverId)
            => new()
            {
                Id = id,
                SeasonId = seasonId,
                SenderUserId = senderId,
                ReceiverUserId = receiverId
            };

        private static Season CreateSeason(int id, Guid? mentorId)
            => new()
            {
                Id = id,
                MentorId = mentorId
            };

        private static DeleteRequest CreateDeleteRequest(
            int id,
            int feedbackId,
            Guid senderUserId,
            string reason)
            => new()
            {
                Id = id,
                FeedbackId = feedbackId,
                SenderUserId = senderUserId,
                Reason = reason,
                CreatedAtUtc = DateTime.UtcNow
            };

        // -------- CreateAsync --------

        [Fact]
        public async Task CreateAsync_EmptyReason_ThrowsValidation()
        {
            var dto = new CreateDeleteRequestDto
            {
                FeedbackId = 1,
                SenderUserId = Guid.NewGuid(),
                Reason = "   "
            };

            await Assert.ThrowsAsync<AppValidationException>(() =>
                _sut.CreateAsync(dto, CancellationToken.None));
        }

        [Fact]
        public async Task CreateAsync_FeedbackNotFound_ThrowsNotFound()
        {
            var dto = new CreateDeleteRequestDto
            {
                FeedbackId = 5,
                SenderUserId = Guid.NewGuid(),
                Reason = "Because"
            };

            _feedbacks.Setup(r => r.GetByIdAsync(5, It.IsAny<CancellationToken>()))
                .ReturnsAsync((Feedback?)null);

            await Assert.ThrowsAsync<NotFoundException>(() =>
                _sut.CreateAsync(dto, CancellationToken.None));
        }

        [Fact]
        public async Task CreateAsync_SenderNotParticipantAndNotSeasonMentor_ThrowsForbidden()
        {
            var sender = Guid.NewGuid();
            var fb = CreateFeedback(
                id: 10,
                seasonId: 3,
                senderId: Guid.NewGuid(),
                receiverId: Guid.NewGuid()); // oba različita od sender-a

            _feedbacks.Setup(r => r.GetByIdAsync(10, It.IsAny<CancellationToken>()))
                .ReturnsAsync(fb);

            var season = CreateSeason(3, mentorId: Guid.NewGuid()); // mentor također različit
            _seasons.Setup(r => r.GetByIdAsync(3, It.IsAny<CancellationToken>(), false))
                .ReturnsAsync(season);

            var dto = new CreateDeleteRequestDto
            {
                FeedbackId = 10,
                SenderUserId = sender,
                Reason = "Some reason"
            };

            await Assert.ThrowsAsync<ForbiddenException>(() =>
                _sut.CreateAsync(dto, CancellationToken.None));
        }

        [Fact]
        public async Task CreateAsync_SenderIsParticipant_CreatesRequestAndReturnsId()
        {
            var sender = Guid.NewGuid();
            var fb = CreateFeedback(
                id: 10,
                seasonId: 3,
                senderId: sender,
                receiverId: Guid.NewGuid());

            _feedbacks.Setup(r => r.GetByIdAsync(10, It.IsAny<CancellationToken>()))
                .ReturnsAsync(fb);

            var season = CreateSeason(3, mentorId: Guid.NewGuid());
            _seasons.Setup(r => r.GetByIdAsync(3, It.IsAny<CancellationToken>(), false))
                .ReturnsAsync(season);

            DeleteRequest? added = null;
            _deleteRequests.Setup(r => r.AddAsync(It.IsAny<DeleteRequest>(), It.IsAny<CancellationToken>()))
                .Callback<DeleteRequest, CancellationToken>((dr, _) =>
                {
                    added = dr;
                    dr.Id = 42;
                })
                .Returns(Task.CompletedTask);

            _deleteRequests.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var dto = new CreateDeleteRequestDto
            {
                FeedbackId = 10,
                SenderUserId = sender,
                Reason = "  Please delete  "
            };

            var id = await _sut.CreateAsync(dto, CancellationToken.None);

            Assert.Equal(42, id);
            Assert.NotNull(added);
            Assert.Equal(10, added!.FeedbackId);
            Assert.Equal(sender, added.SenderUserId);
            Assert.Equal("Please delete", added.Reason);
        }

        [Fact]
        public async Task CreateAsync_SenderIsSeasonMentor_CreatesRequest()
        {
            var sender = Guid.NewGuid();
            var fb = CreateFeedback(
                id: 10,
                seasonId: 3,
                senderId: Guid.NewGuid(),
                receiverId: Guid.NewGuid()); // nije participant

            _feedbacks.Setup(r => r.GetByIdAsync(10, It.IsAny<CancellationToken>()))
                .ReturnsAsync(fb);

            var season = CreateSeason(3, mentorId: sender); // ali je mentor sezone
            _seasons.Setup(r => r.GetByIdAsync(3, It.IsAny<CancellationToken>(), false))
                .ReturnsAsync(season);

            DeleteRequest? added = null;
            _deleteRequests.Setup(r => r.AddAsync(It.IsAny<DeleteRequest>(), It.IsAny<CancellationToken>()))
                .Callback<DeleteRequest, CancellationToken>((dr, _) =>
                {
                    added = dr;
                    dr.Id = 7;
                })
                .Returns(Task.CompletedTask);
            _deleteRequests.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var dto = new CreateDeleteRequestDto
            {
                FeedbackId = 10,
                SenderUserId = sender,
                Reason = "Reason"
            };

            var id = await _sut.CreateAsync(dto, CancellationToken.None);

            Assert.Equal(7, id);
            Assert.NotNull(added);
            Assert.Equal(sender, added!.SenderUserId);
        }

        // -------- GetAllAsync --------

        [Fact]
        public async Task GetAllAsync_MapsEntitiesToDtos()
        {
            var dr1 = CreateDeleteRequest(1, 10, Guid.NewGuid(), "R1");
            var dr2 = CreateDeleteRequest(2, 11, Guid.NewGuid(), "R2");

            _deleteRequests.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<DeleteRequest> { dr1, dr2 });

            var list = await _sut.GetAllAsync(CancellationToken.None);

            Assert.Equal(2, list.Count);
            Assert.Contains(list, x => x.Id == 1 && x.Reason == "R1" && x.FeedbackId == 10);
            Assert.Contains(list, x => x.Id == 2 && x.Reason == "R2" && x.FeedbackId == 11);
        }

        // -------- ApproveAsync --------

        [Fact]
        public async Task ApproveAsync_RequestNotFound_ThrowsNotFound()
        {
            _deleteRequests.Setup(r => r.GetByIdAsync(5, It.IsAny<CancellationToken>()))
                .ReturnsAsync((DeleteRequest?)null);

            await Assert.ThrowsAsync<NotFoundException>(() =>
                _sut.ApproveAsync(5, CancellationToken.None));
        }

        [Fact]
        public async Task ApproveAsync_WithFeedback_DeletesFeedbackAndRequest()
        {
            var dr = CreateDeleteRequest(1, feedbackId: 10, senderUserId: Guid.NewGuid(), reason: "R");
            var fb = CreateFeedback(10, 3, Guid.NewGuid(), Guid.NewGuid());

            _deleteRequests.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(dr);
            _feedbacks.Setup(r => r.GetByIdAsync(10, It.IsAny<CancellationToken>()))
                .ReturnsAsync(fb);

            _feedbacks.Setup(r => r.DeleteAsync(fb, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _deleteRequests.Setup(r => r.DeleteAsync(dr, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _deleteRequests.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            await _sut.ApproveAsync(1, CancellationToken.None);

            _feedbacks.Verify(r => r.DeleteAsync(fb, It.IsAny<CancellationToken>()), Times.Once);
            _deleteRequests.Verify(r => r.DeleteAsync(dr, It.IsAny<CancellationToken>()), Times.Once);
            _deleteRequests.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ApproveAsync_FeedbackAlreadyMissing_DeletesOnlyRequest()
        {
            var dr = CreateDeleteRequest(1, feedbackId: 10, senderUserId: Guid.NewGuid(), reason: "R");

            _deleteRequests.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(dr);
            _feedbacks.Setup(r => r.GetByIdAsync(10, It.IsAny<CancellationToken>()))
                .ReturnsAsync((Feedback?)null);

            _deleteRequests.Setup(r => r.DeleteAsync(dr, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _deleteRequests.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            await _sut.ApproveAsync(1, CancellationToken.None);

            _feedbacks.Verify(r => r.DeleteAsync(It.IsAny<Feedback>(), It.IsAny<CancellationToken>()), Times.Never);
            _deleteRequests.Verify(r => r.DeleteAsync(dr, It.IsAny<CancellationToken>()), Times.Once);
        }

        // -------- RejectAsync --------

        [Fact]
        public async Task RejectAsync_RequestNotFound_ThrowsNotFound()
        {
            _deleteRequests.Setup(r => r.GetByIdAsync(3, It.IsAny<CancellationToken>()))
                .ReturnsAsync((DeleteRequest?)null);

            await Assert.ThrowsAsync<NotFoundException>(() =>
                _sut.RejectAsync(3, CancellationToken.None));
        }

        [Fact]
        public async Task RejectAsync_DeletesRequestAndSaves()
        {
            var dr = CreateDeleteRequest(3, feedbackId: 10, senderUserId: Guid.NewGuid(), reason: "R");

            _deleteRequests.Setup(r => r.GetByIdAsync(3, It.IsAny<CancellationToken>()))
                .ReturnsAsync(dr);
            _deleteRequests.Setup(r => r.DeleteAsync(dr, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _deleteRequests.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            await _sut.RejectAsync(3, CancellationToken.None);

            _deleteRequests.Verify(r => r.DeleteAsync(dr, It.IsAny<CancellationToken>()), Times.Once);
            _deleteRequests.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
