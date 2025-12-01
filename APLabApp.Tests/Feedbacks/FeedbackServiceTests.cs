using APLabApp.BLL.Errors;
using APLabApp.BLL.Feedbacks;
using APLabApp.Dal.Entities;
using APLabApp.Dal.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace APLabApp.Tests.Feedbacks
{
    public class FeedbackServiceTests
    {
        private readonly Mock<IUserRepository> _users;
        private readonly Mock<ISeasonRepository> _seasons;
        private readonly Mock<IFeedbackRepository> _feedbacks;
        private readonly Mock<IGradeRepository> _grades;
        private readonly FeedbackService _sut;

        public FeedbackServiceTests()
        {
            _users = new Mock<IUserRepository>();
            _seasons = new Mock<ISeasonRepository>();
            _feedbacks = new Mock<IFeedbackRepository>();
            _grades = new Mock<IGradeRepository>();

            _sut = new FeedbackService(
                _users.Object,
                _seasons.Object,
                _feedbacks.Object,
                _grades.Object);
        }

        private static User CreateUser(Guid id, int? seasonId = null)
            => new User { Id = id, SeasonId = seasonId, FullName = $"User-{id}", Email = $"user-{id}@test.local" };

        private static Season CreateActiveSeason(int id, Guid? mentorId = null)
        {
            var now = DateTime.UtcNow;
            return new Season
            {
                Id = id,
                MentorId = mentorId ?? Guid.NewGuid(),
                StartDate = now.AddDays(-30),
                EndDate = now.AddDays(30)
            };
        }

        private static Season CreateInactiveSeason(int id, Guid? mentorId = null)
        {
            var now = DateTime.UtcNow;
            return new Season
            {
                Id = id,
                MentorId = mentorId ?? Guid.NewGuid(),
                StartDate = now.AddDays(-30),
                EndDate = now.AddDays(-1)
            };
        }

        [Fact]
        public async Task CreateInternFeedbackAsync_SenderDoesNotExist_ThrowsNotFoundException()
        {
            var senderId = Guid.NewGuid();
            var receiverId = Guid.NewGuid();
            var req = new CreateInternFeedbackRequest(receiverId, "test");

            _users.Setup(r => r.GetByIdAsync(senderId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((User?)null);

            await Assert.ThrowsAsync<NotFoundException>(() =>
                _sut.CreateInternFeedbackAsync(senderId, req, CancellationToken.None));
        }

        [Fact]
        public async Task CreateInternFeedbackAsync_SenderWithoutSeason_ThrowsForbiddenException()
        {
            var senderId = Guid.NewGuid();
            var receiverId = Guid.NewGuid();
            var req = new CreateInternFeedbackRequest(receiverId, "test");
            var sender = CreateUser(senderId, null);

            _users.Setup(r => r.GetByIdAsync(senderId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(sender);

            await Assert.ThrowsAsync<ForbiddenException>(() =>
                _sut.CreateInternFeedbackAsync(senderId, req, CancellationToken.None));
        }

        [Fact]
        public async Task CreateInternFeedbackAsync_SeasonNotActive_ThrowsAppValidationException()
        {
            var senderId = Guid.NewGuid();
            var receiverId = Guid.NewGuid();
            var req = new CreateInternFeedbackRequest(receiverId, "test");
            var sender = CreateUser(senderId, 1);
            var season = CreateInactiveSeason(1);

            _users.Setup(r => r.GetByIdAsync(senderId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(sender);
            _seasons.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>(), false))
                .ReturnsAsync(season);

            await Assert.ThrowsAsync<AppValidationException>(() =>
                _sut.CreateInternFeedbackAsync(senderId, req, CancellationToken.None));
        }

        [Fact]
        public async Task CreateInternFeedbackAsync_ReceiverDoesNotExist_ThrowsNotFoundException()
        {
            var senderId = Guid.NewGuid();
            var receiverId = Guid.NewGuid();
            var req = new CreateInternFeedbackRequest(receiverId, "test");
            var sender = CreateUser(senderId, 1);
            var season = CreateActiveSeason(1);

            _users.Setup(r => r.GetByIdAsync(senderId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(sender);
            _seasons.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>(), false))
                .ReturnsAsync(season);
            _users.Setup(r => r.GetByIdAsync(receiverId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((User?)null);

            await Assert.ThrowsAsync<NotFoundException>(() =>
                _sut.CreateInternFeedbackAsync(senderId, req, CancellationToken.None));
        }

        [Fact]
        public async Task CreateInternFeedbackAsync_ReceiverSameAsSender_ThrowsAppValidationException()
        {
            var senderId = Guid.NewGuid();
            var req = new CreateInternFeedbackRequest(senderId, "test");
            var sender = CreateUser(senderId, 1);
            var season = CreateActiveSeason(1);

            _users.Setup(r => r.GetByIdAsync(senderId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(sender);
            _seasons.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>(), false))
                .ReturnsAsync(season);
            _users.Setup(r => r.GetByIdAsync(senderId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(sender);

            await Assert.ThrowsAsync<AppValidationException>(() =>
                _sut.CreateInternFeedbackAsync(senderId, req, CancellationToken.None));
        }

        [Fact]
        public async Task CreateInternFeedbackAsync_ReceiverNotInternInSameSeasonOrMentor_ThrowsForbiddenException()
        {
            var senderId = Guid.NewGuid();
            var receiverId = Guid.NewGuid();
            var req = new CreateInternFeedbackRequest(receiverId, "test");
            var sender = CreateUser(senderId, 1);
            var season = CreateActiveSeason(1, Guid.NewGuid());
            var receiver = CreateUser(receiverId, 2);

            _users.Setup(r => r.GetByIdAsync(senderId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(sender);
            _seasons.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>(), false))
                .ReturnsAsync(season);
            _users.Setup(r => r.GetByIdAsync(receiverId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(receiver);

            await Assert.ThrowsAsync<ForbiddenException>(() =>
                _sut.CreateInternFeedbackAsync(senderId, req, CancellationToken.None));
        }

        [Fact]
        public async Task CreateInternFeedbackAsync_ValidRequest_CreatesFeedback()
        {
            var senderId = Guid.NewGuid();
            var receiverId = Guid.NewGuid();
            var req = new CreateInternFeedbackRequest(receiverId, "  hello  ");
            var sender = CreateUser(senderId, 1);
            var season = CreateActiveSeason(1, receiverId);
            var receiver = CreateUser(receiverId, 1);
            Feedback? savedFeedback = null;

            _users.Setup(r => r.GetByIdAsync(senderId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(sender);
            _seasons.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>(), false))
                .ReturnsAsync(season);
            _users.Setup(r => r.GetByIdAsync(receiverId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(receiver);
            _feedbacks.Setup(r => r.AddAsync(It.IsAny<Feedback>(), It.IsAny<CancellationToken>()))
                .Callback<Feedback, CancellationToken>((f, _) => savedFeedback = f)
                .Returns(Task.CompletedTask);

            var dto = await _sut.CreateInternFeedbackAsync(senderId, req, CancellationToken.None);

            Assert.NotNull(savedFeedback);
            Assert.Equal(senderId, savedFeedback!.SenderUserId);
            Assert.Equal(receiverId, savedFeedback.ReceiverUserId);
            Assert.Equal("hello", savedFeedback.Comment);
            Assert.Equal(dto.SeasonId, savedFeedback.SeasonId);
            _feedbacks.Verify(r => r.AddAsync(It.IsAny<Feedback>(), It.IsAny<CancellationToken>()), Times.Once);
            _feedbacks.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task CreateMentorFeedbackAsync_MentorDoesNotExist_ThrowsNotFoundException()
        {
            var mentorId = Guid.NewGuid();
            var internId = Guid.NewGuid();
            var req = new CreateMentorFeedbackRequest(internId, "test", 3, 4, 5);

            _users.Setup(r => r.GetByIdAsync(mentorId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((User?)null);

            await Assert.ThrowsAsync<NotFoundException>(() =>
                _sut.CreateMentorFeedbackAsync(mentorId, req, CancellationToken.None));
        }

        [Fact]
        public async Task CreateMentorFeedbackAsync_ReceiverDoesNotExist_ThrowsNotFoundException()
        {
            var mentorId = Guid.NewGuid();
            var internId = Guid.NewGuid();
            var req = new CreateMentorFeedbackRequest(internId, "test", 3, 4, 5);
            var mentor = CreateUser(mentorId, null);

            _users.Setup(r => r.GetByIdAsync(mentorId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(mentor);
            _users.Setup(r => r.GetByIdAsync(internId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((User?)null);

            await Assert.ThrowsAsync<NotFoundException>(() =>
                _sut.CreateMentorFeedbackAsync(mentorId, req, CancellationToken.None));
        }

        [Fact]
        public async Task CreateMentorFeedbackAsync_SeasonNotActive_ThrowsAppValidationException()
        {
            var mentorId = Guid.NewGuid();
            var internId = Guid.NewGuid();
            var req = new CreateMentorFeedbackRequest(internId, "test", 3, 4, 5);
            var mentor = CreateUser(mentorId, null);
            var intern = CreateUser(internId, 1);
            var season = CreateInactiveSeason(1, mentorId);

            _users.Setup(r => r.GetByIdAsync(mentorId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(mentor);
            _users.Setup(r => r.GetByIdAsync(internId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(intern);
            _seasons.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>(), false))
                .ReturnsAsync(season);

            await Assert.ThrowsAsync<AppValidationException>(() =>
                _sut.CreateMentorFeedbackAsync(mentorId, req, CancellationToken.None));
        }

        [Fact]
        public async Task CreateMentorFeedbackAsync_ReceiverHasNoSeason_ThrowsForbiddenException()
        {
            var mentorId = Guid.NewGuid();
            var internId = Guid.NewGuid();
            var req = new CreateMentorFeedbackRequest(internId, "test", 3, 4, 5);
            var mentor = CreateUser(mentorId, null);
            var intern = CreateUser(internId, null);

            _users.Setup(r => r.GetByIdAsync(mentorId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(mentor);
            _users.Setup(r => r.GetByIdAsync(internId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(intern);

            await Assert.ThrowsAsync<ForbiddenException>(() =>
                _sut.CreateMentorFeedbackAsync(mentorId, req, CancellationToken.None));
        }

        [Fact]
        public async Task CreateMentorFeedbackAsync_MentorNotOwnerOfSeason_ThrowsForbiddenException()
        {
            var mentorId = Guid.NewGuid();
            var internId = Guid.NewGuid();
            var req = new CreateMentorFeedbackRequest(internId, "test", 3, 4, 5);
            var mentor = CreateUser(mentorId, null);
            var intern = CreateUser(internId, 1);
            var season = CreateActiveSeason(1, Guid.NewGuid());

            _users.Setup(r => r.GetByIdAsync(mentorId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(mentor);
            _users.Setup(r => r.GetByIdAsync(internId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(intern);
            _seasons.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>(), false))
                .ReturnsAsync(season);

            await Assert.ThrowsAsync<ForbiddenException>(() =>
                _sut.CreateMentorFeedbackAsync(mentorId, req, CancellationToken.None));
        }

        [Fact]
        public async Task CreateMentorFeedbackAsync_ValidRequest_CreatesFeedbackAndGrade()
        {
            var mentorId = Guid.NewGuid();
            var internId = Guid.NewGuid();
            var req = new CreateMentorFeedbackRequest(internId, "  good job  ", 3, 4, 5);
            var mentor = CreateUser(mentorId, null);
            var intern = CreateUser(internId, 1);
            var season = CreateActiveSeason(1, mentorId);
            Feedback? savedFeedback = null;
            Grade? savedGrade = null;

            _users.Setup(r => r.GetByIdAsync(mentorId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(mentor);
            _users.Setup(r => r.GetByIdAsync(internId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(intern);
            _seasons.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>(), false))
                .ReturnsAsync(season);
            _feedbacks.Setup(r => r.AddAsync(It.IsAny<Feedback>(), It.IsAny<CancellationToken>()))
                .Callback<Feedback, CancellationToken>((f, _) => savedFeedback = f)
                .Returns(Task.CompletedTask);
            _grades.Setup(r => r.AddAsync(It.IsAny<Grade>(), It.IsAny<CancellationToken>()))
                .Callback<Grade, CancellationToken>((g, _) => savedGrade = g)
                .Returns(Task.CompletedTask);

            var dto = await _sut.CreateMentorFeedbackAsync(mentorId, req, CancellationToken.None);

            Assert.NotNull(savedFeedback);
            Assert.NotNull(savedGrade);
            Assert.Equal(mentorId, savedFeedback!.SenderUserId);
            Assert.Equal(internId, savedFeedback.ReceiverUserId);
            Assert.Equal("good job", savedFeedback.Comment);
            Assert.Same(savedFeedback, savedGrade!.Feedback);
            Assert.Equal(req.CareerSkills, savedGrade.CareerSkills);
            Assert.Equal(req.Communication, savedGrade.Communication);
            Assert.Equal(req.Collaboration, savedGrade.Collaboration);
            Assert.NotNull(dto.Grade);
            _feedbacks.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task DeleteAsync_FeedbackDoesNotExist_ThrowsNotFoundException()
        {
            _feedbacks.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync((Feedback?)null);

            await Assert.ThrowsAsync<NotFoundException>(() =>
                _sut.DeleteAsync(1, CancellationToken.None));
        }

        [Fact]
        public async Task DeleteAsync_FeedbackExists_DeletesFeedback()
        {
            var feedback = new Feedback { Id = 5 };

            _feedbacks.Setup(r => r.GetByIdAsync(5, It.IsAny<CancellationToken>()))
                .ReturnsAsync(feedback);

            await _sut.DeleteAsync(5, CancellationToken.None);

            _feedbacks.Verify(r => r.DeleteAsync(feedback, It.IsAny<CancellationToken>()), Times.Once);
            _feedbacks.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetForInternAsync_UserDoesNotExist_ThrowsNotFoundException()
        {
            var internId = Guid.NewGuid();

            _users.Setup(r => r.GetByIdAsync(internId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((User?)null);

            await Assert.ThrowsAsync<NotFoundException>(() =>
                _sut.GetForInternAsync(internId, 1, 10, CancellationToken.None));
        }

        [Fact]
        public async Task GetForMentorAsync_MentorDoesNotExist_ThrowsNotFoundException()
        {
            var mentorId = Guid.NewGuid();

            _users.Setup(r => r.GetByIdAsync(mentorId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((User?)null);

            await Assert.ThrowsAsync<NotFoundException>(() =>
                _sut.GetForMentorAsync(mentorId, 1, 10, CancellationToken.None));
        }

        [Fact]
        public async Task GetMentorMonthlyAveragesPagedAsync_MentorDoesNotExist_ThrowsNotFoundException()
        {
            var mentorId = Guid.NewGuid();

            _users.Setup(r => r.GetByIdAsync(mentorId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((User?)null);

            await Assert.ThrowsAsync<NotFoundException>(() =>
                _sut.GetMentorMonthlyAveragesPagedAsync(mentorId, 1, 1, null, null, 1, 10, CancellationToken.None));
        }

        [Fact]
        public async Task GetMentorMonthlyAveragesPagedAsync_SeasonDoesNotExist_ThrowsNotFoundException()
        {
            var mentorId = Guid.NewGuid();
            var mentor = CreateUser(mentorId, null);

            _users.Setup(r => r.GetByIdAsync(mentorId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(mentor);
            _seasons.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>(), false))
                .ReturnsAsync((Season?)null);

            await Assert.ThrowsAsync<NotFoundException>(() =>
                _sut.GetMentorMonthlyAveragesPagedAsync(mentorId, 1, 1, null, null, 1, 10, CancellationToken.None));
        }

        [Fact]
        public async Task GetMentorMonthlyAveragesPagedAsync_MentorNotOwnerOfSeason_ThrowsForbiddenException()
        {
            var mentorId = Guid.NewGuid();
            var mentor = CreateUser(mentorId, null);
            var season = CreateActiveSeason(1, Guid.NewGuid());

            _users.Setup(r => r.GetByIdAsync(mentorId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(mentor);
            _seasons.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>(), false))
                .ReturnsAsync(season);

            await Assert.ThrowsAsync<ForbiddenException>(() =>
                _sut.GetMentorMonthlyAveragesPagedAsync(mentorId, 1, 1, null, null, 1, 10, CancellationToken.None));
        }

        [Fact]
        public async Task SearchForMentorAsync_SeasonDoesNotExist_ThrowsNotFoundException()
        {
            var mentorId = Guid.NewGuid();

            _seasons.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>(), false))
                .ReturnsAsync((Season?)null);

            await Assert.ThrowsAsync<NotFoundException>(() =>
                _sut.SearchForMentorAsync(mentorId, 1, null, null, null, 1, 10, CancellationToken.None));
        }

        [Fact]
        public async Task SearchForMentorAsync_MentorNotOwnerOfSeason_ThrowsForbiddenException()
        {
            var mentorId = Guid.NewGuid();
            var season = CreateActiveSeason(1, Guid.NewGuid());

            _seasons.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>(), false))
                .ReturnsAsync(season);

            await Assert.ThrowsAsync<ForbiddenException>(() =>
                _sut.SearchForMentorAsync(mentorId, 1, null, null, null, 1, 10, CancellationToken.None));
        }

        [Fact]
        public async Task GetSeasonMonthSpansAsync_SeasonDoesNotExist_ThrowsNotFoundException()
        {
            _seasons.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>(), false))
                .ReturnsAsync((Season?)null);

            await Assert.ThrowsAsync<NotFoundException>(() =>
                _sut.GetSeasonMonthSpansAsync(1, CancellationToken.None));
        }

        [Fact]
        public async Task GetForInternAsync_ReturnsPagedAndSortedFeedback()
        {
            var internId = Guid.NewGuid();
            var otherId = Guid.NewGuid();
            var user = CreateUser(internId, 1);

            _users.Setup(r => r.GetByIdAsync(internId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);

            var feedbacks = new List<Feedback>
            {
                new Feedback
                {
                    Id = 1,
                    SeasonId = 1,
                    SenderUserId = internId,
                    ReceiverUserId = otherId,
                    Comment = "a",
                    CreatedAtUtc = new DateTime(2024, 1, 1),
                    Grade = null
                },
                new Feedback
                {
                    Id = 2,
                    SeasonId = 1,
                    SenderUserId = otherId,
                    ReceiverUserId = internId,
                    Comment = "b",
                    CreatedAtUtc = new DateTime(2024, 1, 3),
                    Grade = new Grade { CareerSkills = 5, Communication = 4, Collaboration = 3 }
                },
                new Feedback
                {
                    Id = 3,
                    SeasonId = 1,
                    SenderUserId = otherId,
                    ReceiverUserId = otherId,
                    Comment = "c",
                    CreatedAtUtc = new DateTime(2024, 1, 2),
                    Grade = null
                }
            }.AsTestAsyncQueryable();

            _feedbacks.Setup(r => r.Query()).Returns(feedbacks);

            var result = await _sut.GetForInternAsync(internId, 1, 10, CancellationToken.None);

            Assert.Equal(2, result.Count);
            Assert.Equal(2, result[0].Id);
            Assert.NotNull(result[0].Grade);
            Assert.Equal(1, result[1].Id);
            Assert.Null(result[1].Grade);
        }

        [Fact]
        public async Task GetForInternAsync_NormalizesPageParameters()
        {
            var internId = Guid.NewGuid();
            var otherId = Guid.NewGuid();
            var user = CreateUser(internId, 1);
            _users.Setup(r => r.GetByIdAsync(internId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);

            var list = Enumerable.Range(1, 20)
                .Select(i => new Feedback
                {
                    Id = i,
                    SeasonId = 1,
                    SenderUserId = internId,
                    ReceiverUserId = otherId,
                    Comment = $"c{i}",
                    CreatedAtUtc = new DateTime(2024, 1, 1).AddDays(i),
                    Grade = null
                })
                .AsTestAsyncQueryable();

            _feedbacks.Setup(r => r.Query()).Returns(list);

            var result = await _sut.GetForInternAsync(internId, 0, 0, CancellationToken.None);

            Assert.Equal(10, result.Count);
        }

        [Fact]
        public async Task GetForMentorAsync_ReturnsPagedAndSortedFeedback()
        {
            var mentorId = Guid.NewGuid();
            var internId = Guid.NewGuid();
            var mentor = CreateUser(mentorId, null);
            var season = CreateActiveSeason(1, mentorId);

            _users.Setup(r => r.GetByIdAsync(mentorId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(mentor);

            var feedbacks = new List<Feedback>
            {
                new Feedback
                {
                    Id = 1,
                    SeasonId = 1,
                    Season = season,
                    SenderUserId = mentorId,
                    ReceiverUserId = internId,
                    Comment = "f1",
                    CreatedAtUtc = new DateTime(2024, 1, 2),
                    Grade = new Grade { CareerSkills = 4, Communication = 4, Collaboration = 4 }
                },
                new Feedback
                {
                    Id = 2,
                    SeasonId = 1,
                    Season = season,
                    SenderUserId = mentorId,
                    ReceiverUserId = internId,
                    Comment = "f2",
                    CreatedAtUtc = new DateTime(2024, 1, 3),
                    Grade = null
                },
                new Feedback
                {
                    Id = 3,
                    SeasonId = 2,
                    Season = new Season { Id = 2, MentorId = Guid.NewGuid(), StartDate = DateTime.UtcNow.AddDays(-10), EndDate = DateTime.UtcNow.AddDays(10) },
                    SenderUserId = mentorId,
                    ReceiverUserId = internId,
                    Comment = "other",
                    CreatedAtUtc = new DateTime(2024, 1, 4)
                }
            }.AsTestAsyncQueryable();

            _feedbacks.Setup(r => r.Query()).Returns(feedbacks);

            var result = await _sut.GetForMentorAsync(mentorId, 1, 10, CancellationToken.None);

            Assert.Equal(2, result.Count);
            Assert.Equal(2, result[0].Id);
            Assert.Equal(1, result[1].Id);
        }

        [Fact]
        public async Task GetForAdminAsync_ReturnsAllWhenSeasonNotSpecified()
        {
            var feedbacks = new List<Feedback>
            {
                new Feedback { Id = 1, SeasonId = 1, Comment = "s1", CreatedAtUtc = new DateTime(2024,1,1) },
                new Feedback { Id = 2, SeasonId = 2, Comment = "s2", CreatedAtUtc = new DateTime(2024,1,2) }
            }.AsTestAsyncQueryable();

            _feedbacks.Setup(r => r.Query()).Returns(feedbacks);

            var result = await _sut.GetForAdminAsync(null, 1, 10, CancellationToken.None);

            Assert.Equal(2, result.Count);
            Assert.Equal(2, result[0].Id);
            Assert.Equal(1, result[1].Id);
        }

        [Fact]
        public async Task GetForAdminAsync_FiltersBySeason()
        {
            var feedbacks = new List<Feedback>
            {
                new Feedback { Id = 1, SeasonId = 1, Comment = "s1", CreatedAtUtc = new DateTime(2024,1,1) },
                new Feedback { Id = 2, SeasonId = 2, Comment = "s2", CreatedAtUtc = new DateTime(2024,1,2) }
            }.AsTestAsyncQueryable();

            _feedbacks.Setup(r => r.Query()).Returns(feedbacks);

            var result = await _sut.GetForAdminAsync(1, 1, 10, CancellationToken.None);

            Assert.Single(result);
            Assert.Equal(1, result[0].Id);
        }

        [Fact]
        public async Task GetReceivedFromMentorAsync_FiltersByReceiverAndGraded()
        {
            var internId = Guid.NewGuid();
            var mentorId = Guid.NewGuid();
            var otherId = Guid.NewGuid();

            var season = CreateActiveSeason(1, mentorId);

            var feedbacks = new List<Feedback>
            {
                new Feedback { Id = 1, SeasonId = 1, Season = season, SenderUserId = mentorId, ReceiverUserId = internId, CreatedAtUtc = new DateTime(2024,1,2), Grade = new Grade { CareerSkills = 5, Communication = 5, Collaboration = 5 } },
                new Feedback { Id = 2, SeasonId = 1, Season = season, SenderUserId = otherId, ReceiverUserId = internId, CreatedAtUtc = new DateTime(2024,1,3), Grade = new Grade { CareerSkills = 4, Communication = 4, Collaboration = 4 } },
                new Feedback { Id = 3, SeasonId = 1, Season = season, SenderUserId = mentorId, ReceiverUserId = internId, CreatedAtUtc = new DateTime(2024,1,1), Grade = null },
                new Feedback { Id = 4, SeasonId = 1, Season = season, SenderUserId = mentorId, ReceiverUserId = otherId, CreatedAtUtc = new DateTime(2024,1,4), Grade = new Grade { CareerSkills = 3, Communication = 3, Collaboration = 3 } }
            }.AsTestAsyncQueryable();

            _feedbacks.Setup(r => r.Query()).Returns(feedbacks);

            var result = await _sut.GetReceivedFromMentorAsync(internId, CancellationToken.None);

            Assert.Equal(2, result.Count);
            Assert.Equal(2, result[0].Id);
            Assert.Equal(1, result[1].Id);
            Assert.NotNull(result[0].Grade);
        }

        [Fact]
        public async Task GetReceivedFromInternsAsync_FiltersByReceiverAndUngraded()
        {
            var internId = Guid.NewGuid();
            var otherId = Guid.NewGuid();

            var feedbacks = new List<Feedback>
            {
                new Feedback { Id = 1, SeasonId = 1, SenderUserId = otherId, ReceiverUserId = internId, CreatedAtUtc = new DateTime(2024,1,2), Grade = null },
                new Feedback { Id = 2, SeasonId = 1, SenderUserId = otherId, ReceiverUserId = internId, CreatedAtUtc = new DateTime(2024,1,3), Grade = new Grade() },
                new Feedback { Id = 3, SeasonId = 1, SenderUserId = internId, ReceiverUserId = otherId, CreatedAtUtc = new DateTime(2024,1,1), Grade = null }
            }.AsTestAsyncQueryable();

            _feedbacks.Setup(r => r.Query()).Returns(feedbacks);

            var result = await _sut.GetReceivedFromInternsAsync(internId, CancellationToken.None);

            Assert.Single(result);
            Assert.Equal(1, result[0].Id);
        }

        [Fact]
        public async Task GetSentByMeAsync_FiltersBySender()
        {
            var userId = Guid.NewGuid();
            var otherId = Guid.NewGuid();

            var feedbacks = new List<Feedback>
            {
                new Feedback { Id = 1, SeasonId = 1, SenderUserId = userId, ReceiverUserId = otherId, Comment = "mine1", CreatedAtUtc = new DateTime(2024,1,2), Grade = null },
                new Feedback { Id = 2, SeasonId = 1, SenderUserId = otherId, ReceiverUserId = userId, Comment = "not mine", CreatedAtUtc = new DateTime(2024,1,3), Grade = null },
                new Feedback { Id = 3, SeasonId = 1, SenderUserId = userId, ReceiverUserId = otherId, Comment = "mine2", CreatedAtUtc = new DateTime(2024,1,4), Grade = new Grade() }
            }.AsTestAsyncQueryable();

            _feedbacks.Setup(r => r.Query()).Returns(feedbacks);

            var result = await _sut.GetSentByMeAsync(userId, CancellationToken.None);

            Assert.Equal(2, result.Count);
            Assert.Equal(3, result[0].Id);
            Assert.Equal(1, result[1].Id);
        }

        [Fact]
        public async Task GetSeasonMonthSpansAsync_ReturnsCorrectSpans()
        {
            var season = new Season
            {
                Id = 1,
                StartDate = new DateTime(2024, 1, 10, 12, 0, 0, DateTimeKind.Utc),
                EndDate = new DateTime(2024, 3, 5, 0, 0, 0, DateTimeKind.Utc)
            };

            _seasons.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>(), false))
                .ReturnsAsync(season);

            var spans = await _sut.GetSeasonMonthSpansAsync(1, CancellationToken.None);

            Assert.True(spans.Count >= 2);
            Assert.Equal(1, spans[0].Index);
            Assert.True(spans[0].StartUtc <= spans[0].EndUtc);
        }

        [Fact]
        public async Task GetSeasonMonthSpansAsync_ReturnsEmptyWhenEndBeforeOrEqualStart()
        {
            var start = DateTime.UtcNow.Date;
            var season = new Season
            {
                Id = 1,
                StartDate = start.AddHours(12),
                EndDate = start
            };

            _seasons.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>(), false))
                .ReturnsAsync(season);

            var spans = await _sut.GetSeasonMonthSpansAsync(1, CancellationToken.None);

            Assert.Empty(spans);
        }

        [Fact]
        public async Task GetMentorMonthlyAveragesPagedAsync_ReturnsRowsWithAverages()
        {
            var mentorId = Guid.NewGuid();
            var intern1Id = Guid.NewGuid();
            var intern2Id = Guid.NewGuid();

            var mentor = CreateUser(mentorId, null);
            var now = DateTime.UtcNow;
            var start = now.AddMonths(-2);
            var end = now.AddMonths(-1);

            var season = new Season
            {
                Id = 1,
                MentorId = mentorId,
                StartDate = start,
                EndDate = end
            };

            _users.Setup(r => r.GetByIdAsync(mentorId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(mentor);
            _seasons.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>(), false))
                .ReturnsAsync(season);

            var interns = new List<User>
            {
                new User { Id = intern1Id, SeasonId = 1, FullName = "Intern A", Email = "a@test.local" },
                new User { Id = intern2Id, SeasonId = 1, FullName = "Intern B", Email = "b@test.local" },
                new User { Id = mentorId, SeasonId = 1, FullName = "Mentor", Email = "m@test.local" }
            }.AsTestAsyncQueryable();

            _users.Setup(r => r.Query()).Returns(interns);

            var feedbacks = new List<Feedback>
            {
                new Feedback
                {
                    Id = 1,
                    SeasonId = 1,
                    SenderUserId = mentorId,
                    ReceiverUserId = intern1Id,
                    CreatedAtUtc = new DateTime(start.Year, start.Month, start.Day, 1, 0, 0, DateTimeKind.Utc),
                    Grade = new Grade { CareerSkills = 5, Communication = 4, Collaboration = 3 }
                },
                new Feedback
                {
                    Id = 2,
                    SeasonId = 1,
                    SenderUserId = mentorId,
                    ReceiverUserId = intern1Id,
                    CreatedAtUtc = new DateTime(start.Year, start.Month, start.Day, 2, 0, 0, DateTimeKind.Utc),
                    Grade = new Grade { CareerSkills = 4, Communication = 4, Collaboration = 4 }
                },
                new Feedback
                {
                    Id = 3,
                    SeasonId = 1,
                    SenderUserId = mentorId,
                    ReceiverUserId = intern2Id,
                    CreatedAtUtc = end.AddDays(10),
                    Grade = new Grade { CareerSkills = 1, Communication = 1, Collaboration = 1 }
                }
            }.AsTestAsyncQueryable();

            _feedbacks.Setup(r => r.Query()).Returns(feedbacks);

            var page = await _sut.GetMentorMonthlyAveragesPagedAsync(mentorId, 1, 1, null, null, 1, 10, CancellationToken.None);

            Assert.Equal(1, page.SeasonId);
            Assert.Equal(2, page.Items.Count);
            var rowForIntern1 = page.Items.Single(r => r.InternUserId == intern1Id);
            Assert.True(rowForIntern1.AverageScore.HasValue);
            Assert.Equal(2, rowForIntern1.GradedFeedbacksCount);
            var rowForIntern2 = page.Items.Single(r => r.InternUserId == intern2Id);
            Assert.False(rowForIntern2.AverageScore.HasValue);
            Assert.Equal(0, rowForIntern2.GradedFeedbacksCount);
        }

        [Fact]
        public async Task SearchForAdminAsync_FiltersByTypeAndPaginates()
        {
            var mentorId = Guid.NewGuid();
            var intern1Id = Guid.NewGuid();
            var intern2Id = Guid.NewGuid();

            var season = new Season
            {
                Id = 1,
                MentorId = mentorId,
                StartDate = DateTime.UtcNow.AddMonths(-2),
                EndDate = DateTime.UtcNow.AddMonths(1)
            };

            _seasons.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>(), false))
                .ReturnsAsync(season);

            var feedbacks = new List<Feedback>
            {
                new Feedback
                {
                    Id = 1,
                    SeasonId = 1,
                    Season = season,
                    SenderUserId = intern1Id,
                    ReceiverUserId = intern2Id,
                    Comment = "i2i",
                    CreatedAtUtc = DateTime.UtcNow.AddDays(-10),
                    Grade = null
                },
                new Feedback
                {
                    Id = 2,
                    SeasonId = 1,
                    Season = season,
                    SenderUserId = intern1Id,
                    ReceiverUserId = mentorId,
                    Comment = "i2m",
                    CreatedAtUtc = DateTime.UtcNow.AddDays(-9),
                    Grade = null
                },
                new Feedback
                {
                    Id = 3,
                    SeasonId = 1,
                    Season = season,
                    SenderUserId = mentorId,
                    ReceiverUserId = intern2Id,
                    Comment = "m2i",
                    CreatedAtUtc = DateTime.UtcNow.AddDays(-8),
                    Grade = new Grade { CareerSkills = 3, Communication = 3, Collaboration = 3 }
                }
            }.AsTestAsyncQueryable();

            _feedbacks.Setup(r => r.Query()).Returns(feedbacks);

            var paged = await _sut.SearchForAdminAsync(1, "i2i", "desc", null, 1, 10, CancellationToken.None);

            Assert.Single(paged.Items);
            Assert.Equal("i2i", paged.Items[0].Comment);
            Assert.Equal(1, paged.Items[0].Id);
            Assert.Equal(1, paged.Total);
        }
        [Fact]
        public async Task SearchForMentorAsync_FiltersByTypeAndMonth()
        {
            var mentorId = Guid.NewGuid();
            var internId = Guid.NewGuid();

            var seasonStart = new DateTime(2024, 1, 10, 0, 0, 0, DateTimeKind.Utc);
            var seasonEnd = new DateTime(2024, 3, 5, 0, 0, 0, DateTimeKind.Utc);

            var season = new Season
            {
                Id = 1,
                MentorId = mentorId,
                StartDate = seasonStart,
                EndDate = seasonEnd
            };

            _seasons.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>(), false))
                .ReturnsAsync(season);

            var feedbacks = new List<Feedback>
            {
                // unutar DRUGOG mjeseca (februar)
                new Feedback
                {
                    Id = 1,
                    SeasonId = 1,
                    Season = season,
                    SenderUserId = mentorId,
                    ReceiverUserId = internId,
                    Comment = "in second month",
                    CreatedAtUtc = new DateTime(2024, 2, 10, 0, 0, 0, DateTimeKind.Utc),
                    Grade = new Grade { CareerSkills = 4, Communication = 4, Collaboration = 4 }
                },
                // unutar PRVOG mjeseca (januar) – treba biti filtriran van za monthIndex = 2
                new Feedback
                {
                    Id = 2,
                    SeasonId = 1,
                    Season = season,
                    SenderUserId = mentorId,
                    ReceiverUserId = internId,
                    Comment = "in first month",
                    CreatedAtUtc = new DateTime(2024, 1, 20, 0, 0, 0, DateTimeKind.Utc),
                    Grade = new Grade { CareerSkills = 5, Communication = 5, Collaboration = 5 }
                }
            }.AsTestAsyncQueryable();

            _feedbacks.Setup(r => r.Query()).Returns(feedbacks);

            // type = "m2i" → treba da prođe kroz ApplyTypeFilter granu za m2i
            // monthIndex = 2 → drugi mjesec sezone
            var paged = await _sut.SearchForMentorAsync(
                mentorId,
                seasonId: 1,
                type: "m2i",
                sortDir: "desc",
                monthIndex: 2,
                page: 1,
                pageSize: 10,
                CancellationToken.None);

            Assert.Single(paged.Items);
            Assert.Equal("in second month", paged.Items[0].Comment);
            Assert.Equal(1, paged.Items[0].Id);
            Assert.Equal(1, paged.Total);
        }

        [Fact]
        public async Task SearchForAdminAsync_FiltersByType_I2M()
        {
            var mentorId = Guid.NewGuid();
            var intern1Id = Guid.NewGuid();
            var intern2Id = Guid.NewGuid();

            var season = new Season
            {
                Id = 1,
                MentorId = mentorId,
                StartDate = DateTime.UtcNow.AddMonths(-2),
                EndDate = DateTime.UtcNow.AddMonths(1)
            };

            _seasons.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>(), false))
                .ReturnsAsync(season);

            var feedbacks = new List<Feedback>
            {
                // i2i – treba da bude isključen za "i2m"
                new Feedback
                {
                    Id = 1,
                    SeasonId = 1,
                    Season = season,
                    SenderUserId = intern1Id,
                    ReceiverUserId = intern2Id,
                    Comment = "i2i",
                    CreatedAtUtc = DateTime.UtcNow.AddDays(-10),
                    Grade = null
                },
                // i2m – intern -> mentor, bez grade
                new Feedback
                {
                    Id = 2,
                    SeasonId = 1,
                    Season = season,
                    SenderUserId = intern1Id,
                    ReceiverUserId = mentorId,
                    Comment = "i2m",
                    CreatedAtUtc = DateTime.UtcNow.AddDays(-9),
                    Grade = null
                },
                // m2i – treba da bude isključen za "i2m"
                new Feedback
                {
                    Id = 3,
                    SeasonId = 1,
                    Season = season,
                    SenderUserId = mentorId,
                    ReceiverUserId = intern2Id,
                    Comment = "m2i",
                    CreatedAtUtc = DateTime.UtcNow.AddDays(-8),
                    Grade = new Grade { CareerSkills = 3, Communication = 3, Collaboration = 3 }
                }
            }.AsTestAsyncQueryable();

            _feedbacks.Setup(r => r.Query()).Returns(feedbacks);

            var paged = await _sut.SearchForAdminAsync(
                seasonId: 1,
                type: "i2m",
                sortDir: "desc",
                monthIndex: null,
                page: 1,
                pageSize: 10,
                CancellationToken.None);

            Assert.Single(paged.Items);
            Assert.Equal("i2m", paged.Items[0].Comment);
            Assert.Equal(2, paged.Items[0].Id);
            Assert.Equal(1, paged.Total);
        }

        [Fact]
        public async Task SearchForAdminAsync_FiltersByType_M2I()
        {
            var mentorId = Guid.NewGuid();
            var internId = Guid.NewGuid();

            var season = new Season
            {
                Id = 1,
                MentorId = mentorId,
                StartDate = DateTime.UtcNow.AddMonths(-2),
                EndDate = DateTime.UtcNow.AddMonths(1)
            };

            _seasons.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>(), false))
                .ReturnsAsync(season);

            var feedbacks = new List<Feedback>
            {
                // m2i – treba proći
                new Feedback
                {
                    Id = 1,
                    SeasonId = 1,
                    Season = season,
                    SenderUserId = mentorId,
                    ReceiverUserId = internId,
                    Comment = "m2i-ok",
                    CreatedAtUtc = DateTime.UtcNow.AddDays(-5),
                    Grade = new Grade { CareerSkills = 4, Communication = 4, Collaboration = 4 }
                },
                // i2m – treba biti isključen
                new Feedback
                {
                    Id = 2,
                    SeasonId = 1,
                    Season = season,
                    SenderUserId = internId,
                    ReceiverUserId = mentorId,
                    Comment = "i2m",
                    CreatedAtUtc = DateTime.UtcNow.AddDays(-4),
                    Grade = null
                },
                // i2i – treba biti isključen
                new Feedback
                {
                    Id = 3,
                    SeasonId = 1,
                    Season = season,
                    SenderUserId = internId,
                    ReceiverUserId = internId,
                    Comment = "i2i",
                    CreatedAtUtc = DateTime.UtcNow.AddDays(-3),
                    Grade = null
                }
            }.AsTestAsyncQueryable();

            _feedbacks.Setup(r => r.Query()).Returns(feedbacks);

            var paged = await _sut.SearchForAdminAsync(
                seasonId: 1,
                type: "m2i",
                sortDir: "desc",
                monthIndex: null,
                page: 1,
                pageSize: 10,
                CancellationToken.None);

            Assert.Single(paged.Items);
            Assert.Equal("m2i-ok", paged.Items[0].Comment);
            Assert.Equal(1, paged.Items[0].Id);
            Assert.Equal(1, paged.Total);
        }


    }

    internal static class AsyncQueryableExtensions
    {
        public static IQueryable<T> AsTestAsyncQueryable<T>(this IEnumerable<T> source)
        {
            return new TestAsyncEnumerable<T>(source);
        }
    }

    internal class TestAsyncQueryProvider<TEntity> : IAsyncQueryProvider
    {
        private readonly IQueryProvider _inner;

        internal TestAsyncQueryProvider(IQueryProvider inner)
        {
            _inner = inner;
        }

        public IQueryable CreateQuery(Expression expression)
            => new TestAsyncEnumerable<TEntity>(expression);

        public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
            => new TestAsyncEnumerable<TElement>(expression);

        public object Execute(Expression expression)
            => _inner.Execute(expression)!;

        public TResult Execute<TResult>(Expression expression)
            => _inner.Execute<TResult>(expression)!;

        public TResult ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken = default)
        {
            var expectedResultType = typeof(TResult).GetGenericArguments().FirstOrDefault() ?? typeof(TResult);

            var executeMethod = typeof(IQueryProvider)
                .GetMethods()
                .Single(m =>
                    m.Name == nameof(IQueryProvider.Execute) &&
                    m.IsGenericMethodDefinition &&
                    m.GetParameters().Length == 1 &&
                    m.GetParameters()[0].ParameterType == typeof(Expression));

            var genericExecute = executeMethod.MakeGenericMethod(expectedResultType);
            var executionResult = genericExecute.Invoke(_inner, new object[] { expression });

            var fromResultMethod = typeof(Task)
                .GetMethods()
                .Single(m =>
                    m.Name == nameof(Task.FromResult) &&
                    m.IsGenericMethodDefinition);

            var genericFromResult = fromResultMethod.MakeGenericMethod(expectedResultType);
            var taskResult = genericFromResult.Invoke(null, new[] { executionResult });
            return (TResult)taskResult!;
        }
    }

    internal class TestAsyncEnumerable<T> : EnumerableQuery<T>, IAsyncEnumerable<T>, IQueryable<T>
    {
        public TestAsyncEnumerable(IEnumerable<T> enumerable)
            : base(enumerable)
        {
        }

        public TestAsyncEnumerable(Expression expression)
            : base(expression)
        {
        }

        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
            => new TestAsyncEnumerator<T>(this.AsEnumerable().GetEnumerator());

        IQueryProvider IQueryable.Provider => new TestAsyncQueryProvider<T>(this);
    }

    internal class TestAsyncEnumerator<T> : IAsyncEnumerator<T>
    {
        private readonly IEnumerator<T> _inner;

        public TestAsyncEnumerator(IEnumerator<T> inner)
        {
            _inner = inner;
        }

        public T Current => _inner.Current;

        public ValueTask DisposeAsync()
        {
            _inner.Dispose();
            return ValueTask.CompletedTask;
        }

        public ValueTask<bool> MoveNextAsync()
            => new ValueTask<bool>(_inner.MoveNext());
    }
}
