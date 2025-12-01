using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using APLabApp.BLL.Auth;
using APLabApp.BLL.Errors;
using APLabApp.BLL.Seasons;
using APLabApp.BLL.Users;
using APLabApp.Dal.Entities;
using APLabApp.Dal.Repositories;
using Moq;
using Xunit;

namespace APLabApp.Tests.Seasons
{
    public class SeasonServiceTests
    {
        private readonly Mock<ISeasonRepository> _seasons;
        private readonly Mock<IUserRepository> _users;
        private readonly Mock<IKeycloakAdminService> _kc;
        private readonly SeasonService _sut;

        public SeasonServiceTests()
        {
            _seasons = new Mock<ISeasonRepository>();
            _users = new Mock<IUserRepository>();
            _kc = new Mock<IKeycloakAdminService>();
            _sut = new SeasonService(_seasons.Object, _users.Object, _kc.Object);
        }

        private static Season CreateSeason(
            int id,
            string name,
            DateTime start,
            DateTime end,
            Guid? mentorId = null,
            List<User>? users = null)
            => new()
            {
                Id = id,
                Name = name,
                StartDate = start,
                EndDate = end,
                MentorId = mentorId
            };

        private static User CreateUser(
            Guid id,
            Guid keycloakId,
            string fullName = "User",
            int? seasonId = null)
            => new()
            {
                Id = id,
                KeycloakId = keycloakId,
                FullName = fullName,
                Email = $"{fullName.ToLower()}@test.local",
                SeasonId = seasonId
            };

        [Fact]
        public async Task GetAllAsync_ReturnsMappedSeasons()
        {
            var s1 = CreateSeason(1, "S1", DateTime.Today, DateTime.Today.AddMonths(1));
            var s2 = CreateSeason(2, "S2", DateTime.Today.AddMonths(1), DateTime.Today.AddMonths(2));
            _seasons.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Season> { s1, s2 });

            var result = await _sut.GetAllAsync(CancellationToken.None);

            Assert.Equal(2, result.Count);
            Assert.Contains(result, x => x.Id == 1 && x.Name == "S1");
            Assert.Contains(result, x => x.Id == 2 && x.Name == "S2");
        }

        [Fact]
        public async Task GetByIdAsync_NotFound_ReturnsNull()
        {
            _seasons.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>(), It.IsAny<bool>()))
                .ReturnsAsync((Season?)null);

            var dto = await _sut.GetByIdAsync(1, includeUsers: false, CancellationToken.None);

            Assert.Null(dto);
        }

        [Fact]
        public async Task GetByIdAsync_Found_ReturnsMappedDto()
        {
            var s = CreateSeason(5, "Season 5", DateTime.Today, DateTime.Today.AddDays(10));
            _seasons.Setup(r => r.GetByIdAsync(5, It.IsAny<CancellationToken>(), It.IsAny<bool>()))
                .ReturnsAsync(s);

            var dto = await _sut.GetByIdAsync(5, includeUsers: false, CancellationToken.None);

            Assert.NotNull(dto);
            Assert.Equal(5, dto!.Id);
            Assert.Equal("Season 5", dto.Name);
        }

        [Fact]
        public async Task CreateAsync_OverlappingSeason_ThrowsConflict()
        {
            var existing = CreateSeason(
                1,
                "Existing",
                new DateTime(2024, 1, 1),
                new DateTime(2024, 3, 1));
            _seasons.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Season> { existing });

            var req = new CreateSeasonRequest(
                "New",
                new DateTime(2024, 2, 1),
                new DateTime(2024, 4, 1),
                null);

            await Assert.ThrowsAsync<ConflictException>(() =>
                _sut.CreateAsync(req, CancellationToken.None));
        }

        [Fact]
        public async Task CreateAsync_MentorIdSet_MentorNotFound_ThrowsNotFound()
        {
            _seasons.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Season>());
            var mentorId = Guid.NewGuid();
            _users.Setup(r => r.GetByIdAsync(mentorId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((User?)null);

            var req = new CreateSeasonRequest(
                "S",
                DateTime.Today,
                DateTime.Today.AddDays(1),
                mentorId);

            await Assert.ThrowsAsync<NotFoundException>(() =>
                _sut.CreateAsync(req, CancellationToken.None));
        }

        [Fact]
        public async Task CreateAsync_MentorWithoutKeycloak_ThrowsValidation()
        {
            _seasons.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Season>());
            var mentorId = Guid.NewGuid();
            var mentor = CreateUser(mentorId, Guid.Empty);
            _users.Setup(r => r.GetByIdAsync(mentorId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(mentor);

            var req = new CreateSeasonRequest(
                "S",
                DateTime.Today,
                DateTime.Today.AddDays(1),
                mentorId);

            await Assert.ThrowsAsync<AppValidationException>(() =>
                _sut.CreateAsync(req, CancellationToken.None));
        }

        [Fact]
        public async Task CreateAsync_MentorNotInMentorGroup_ThrowsValidation()
        {
            _seasons.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Season>());
            var mentorId = Guid.NewGuid();
            var kcId = Guid.NewGuid();
            var mentor = CreateUser(mentorId, kcId);
            _users.Setup(r => r.GetByIdAsync(mentorId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(mentor);
            _kc.Setup(k => k.IsUserInGroupAsync(kcId, "mentor", It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            var req = new CreateSeasonRequest(
                "S",
                DateTime.Today,
                DateTime.Today.AddDays(1),
                mentorId);

            await Assert.ThrowsAsync<AppValidationException>(() =>
                _sut.CreateAsync(req, CancellationToken.None));
        }

        [Fact]
        public async Task CreateAsync_Valid_WritesSeasonAndReturnsDto()
        {
            _seasons.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Season>());

            var req = new CreateSeasonRequest(
                "  New Season  ",
                DateTime.Today,
                DateTime.Today.AddDays(1),
                null);

            Season? added = null;
            _seasons.Setup(r => r.AddAsync(It.IsAny<Season>(), It.IsAny<CancellationToken>()))
                .Callback<Season, CancellationToken>((s, _) =>
                {
                    added = s;
                    s.Id = 10;
                })
                .Returns(Task.CompletedTask);
            _seasons.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _seasons.Setup(r => r.GetByIdAsync(10, It.IsAny<CancellationToken>(), false))
                .ReturnsAsync(() => added!);

            var dto = await _sut.CreateAsync(req, CancellationToken.None);

            Assert.NotNull(added);
            Assert.Equal("New Season", added!.Name);
            Assert.Equal(10, dto.Id);
            Assert.Equal("New Season", dto.Name);
        }

        [Fact]
        public async Task UpdateAsync_SeasonNotFound_ReturnsNull()
        {
            _seasons.Setup(r => r.GetByIdAsync(5, It.IsAny<CancellationToken>(), false))
                .ReturnsAsync((Season?)null);

            var req = new UpdateSeasonRequest("X", null, null, null);
            var dto = await _sut.UpdateAsync(5, req, CancellationToken.None);

            Assert.Null(dto);
        }

        [Fact]
        public async Task UpdateAsync_InvalidDateRange_ThrowsValidation()
        {
            var s = CreateSeason(1, "S", DateTime.Today, DateTime.Today.AddDays(5));
            _seasons.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>(), false))
                .ReturnsAsync(s);

            var req = new UpdateSeasonRequest(null, DateTime.Today.AddDays(10), null, null);

            await Assert.ThrowsAsync<AppValidationException>(() =>
                _sut.UpdateAsync(1, req, CancellationToken.None));
        }

        [Fact]
        public async Task UpdateAsync_ChangeNameAndDatesAndMentor_Valid()
        {
            var mentorId = Guid.NewGuid();
            var kcId = Guid.NewGuid();
            var mentor = CreateUser(mentorId, kcId);
            var s = CreateSeason(
                1,
                "Old",
                DateTime.Today,
                DateTime.Today.AddDays(5),
                mentorId: null);

            _seasons.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>(), false))
                .ReturnsAsync(s);
            _users.Setup(r => r.GetByIdAsync(mentorId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(mentor);
            _kc.Setup(k => k.IsUserInGroupAsync(kcId, "mentor", It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            _seasons.Setup(r => r.UpdateAsync(s, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _seasons.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _seasons.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>(), false))
                .ReturnsAsync(s);

            var req = new UpdateSeasonRequest(
                "  New Name ",
                DateTime.Today.AddDays(1),
                DateTime.Today.AddDays(10),
                mentorId);

            var dto = await _sut.UpdateAsync(1, req, CancellationToken.None);

            Assert.NotNull(dto);
            Assert.Equal("New Name", s.Name);
            Assert.Equal(mentorId, s.MentorId);
            Assert.Equal(dto!.Name, s.Name);
        }

        [Fact]
        public async Task DeleteAsync_NotFound_ReturnsFalse()
        {
            _seasons.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>(), true))
                .ReturnsAsync((Season?)null);

            var ok = await _sut.DeleteAsync(1, CancellationToken.None);

            Assert.False(ok);
        }

        [Fact]
        public async Task DeleteAsync_RemovesUsersSeasonIdAndDeletesSeason()
        {
            var u1 = CreateUser(Guid.NewGuid(), Guid.NewGuid(), "U1", seasonId: 3);
            var u2 = CreateUser(Guid.NewGuid(), Guid.NewGuid(), "U2", seasonId: 3);

            var s = CreateSeason(3, "S3", DateTime.Today, DateTime.Today.AddDays(1));
            s.Users.Add(u1);
            s.Users.Add(u2);

            _seasons.Setup(r => r.GetByIdAsync(3, It.IsAny<CancellationToken>(), true))
                .ReturnsAsync(s);
            _seasons.Setup(r => r.DeleteAsync(s, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _seasons.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var ok = await _sut.DeleteAsync(3, CancellationToken.None);

            Assert.True(ok);
            Assert.Null(u1.SeasonId);
            Assert.Null(u2.SeasonId);
            _seasons.Verify(r => r.DeleteAsync(s, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task AssignMentorAsync_SeasonNotFound_ReturnsNotFound()
        {
            _seasons.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>(), false))
                .ReturnsAsync((Season?)null);

            var res = await _sut.AssignMentorAsync(1, Guid.NewGuid(), CancellationToken.None);

            Assert.Equal(AssignMentorResult.NotFound, res);
        }

        [Fact]
        public async Task AssignMentorAsync_ClearMentor_SetsNullAndReturnsOk()
        {
            var s = CreateSeason(1, "S", DateTime.Today, DateTime.Today.AddDays(1), mentorId: Guid.NewGuid());
            _seasons.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>(), false))
                .ReturnsAsync(s);
            _seasons.Setup(r => r.UpdateAsync(s, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _seasons.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var res = await _sut.AssignMentorAsync(1, null, CancellationToken.None);

            Assert.Equal(AssignMentorResult.Ok, res);
            Assert.Null(s.MentorId);
        }

        [Fact]
        public async Task AssignMentorAsync_InvalidMentorRole_ReturnsInvalidRole()
        {
            var s = CreateSeason(1, "S", DateTime.Today, DateTime.Today.AddDays(1));
            _seasons.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>(), false))
                .ReturnsAsync(s);

            var mentorId = Guid.NewGuid();
            var kcId = Guid.NewGuid();
            var mentor = CreateUser(mentorId, kcId);
            _users.Setup(r => r.GetByIdAsync(mentorId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(mentor);
            _kc.Setup(k => k.IsUserInGroupAsync(kcId, "mentor", It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            var res = await _sut.AssignMentorAsync(1, mentorId, CancellationToken.None);

            Assert.Equal(AssignMentorResult.InvalidRole, res);
        }

        [Fact]
        public async Task AddUserAsync_AlreadyInAnotherSeason_ReturnsAlreadyInAnotherSeason()
        {
            var s = CreateSeason(1, "S1", DateTime.Today, DateTime.Today.AddDays(1));
            _seasons.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>(), false))
                .ReturnsAsync(s);

            var userId = Guid.NewGuid();
            var kcId = Guid.NewGuid();
            var user = CreateUser(userId, kcId, "U", seasonId: 2);
            _users.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);
            _kc.Setup(k => k.IsUserInGroupAsync(kcId, "intern", It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var res = await _sut.AddUserAsync(1, userId, CancellationToken.None);

            Assert.Equal(AddUserResult.AlreadyInAnotherSeason, res);
        }

        [Fact]
        public async Task AddUserAsync_Valid_SetsSeasonIdAndSaves()
        {
            var s = CreateSeason(1, "S1", DateTime.Today, DateTime.Today.AddDays(1));
            _seasons.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>(), false))
                .ReturnsAsync(s);

            var userId = Guid.NewGuid();
            var kcId = Guid.NewGuid();
            var user = CreateUser(userId, kcId, "U", seasonId: null);
            _users.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);
            _kc.Setup(k => k.IsUserInGroupAsync(kcId, "intern", It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            _users.Setup(r => r.UpdateAsync(user, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _users.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var res = await _sut.AddUserAsync(1, userId, CancellationToken.None);

            Assert.Equal(AddUserResult.Ok, res);
            Assert.Equal(1, user.SeasonId);
        }

        [Fact]
        public async Task RemoveUserAsync_UserNotInSeason_ReturnsFalse()
        {
            var s = CreateSeason(1, "S1", DateTime.Today, DateTime.Today.AddDays(1));
            _seasons.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>(), false))
                .ReturnsAsync(s);

            var u = CreateUser(Guid.NewGuid(), Guid.NewGuid(), "U", seasonId: 2);
            _users.Setup(r => r.GetByIdAsync(u.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(u);

            var ok = await _sut.RemoveUserAsync(1, u.Id, CancellationToken.None);

            Assert.False(ok);
        }

        [Fact]
        public async Task RemoveUserAsync_Valid_ClearsSeasonAndSaves()
        {
            var s = CreateSeason(1, "S1", DateTime.Today, DateTime.Today.AddDays(1));
            _seasons.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>(), false))
                .ReturnsAsync(s);

            var u = CreateUser(Guid.NewGuid(), Guid.NewGuid(), "U", seasonId: 1);
            _users.Setup(r => r.GetByIdAsync(u.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(u);
            _users.Setup(r => r.UpdateAsync(u, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _users.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var ok = await _sut.RemoveUserAsync(1, u.Id, CancellationToken.None);

            Assert.True(ok);
            Assert.Null(u.SeasonId);
        }

        [Fact]
        public async Task GetUsersAsync_SeasonNotFound_ReturnsEmpty()
        {
            _seasons.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>(), true))
                .ReturnsAsync((Season?)null);

            var list = await _sut.GetUsersAsync(1, CancellationToken.None);

            Assert.Empty(list);
        }

        [Fact]
        public async Task GetUsersAsync_ReturnsMappedUsers()
        {
            var u1 = CreateUser(Guid.NewGuid(), Guid.NewGuid(), "Ana", seasonId: 1);
            var u2 = CreateUser(Guid.NewGuid(), Guid.NewGuid(), "Marko", seasonId: 1);

            var s = CreateSeason(1, "S1", DateTime.Today, DateTime.Today.AddDays(1));
            s.Users.Add(u1);
            s.Users.Add(u2);

            _seasons.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>(), true))
                .ReturnsAsync(s);

            var list = await _sut.GetUsersAsync(1, CancellationToken.None);

            Assert.Equal(2, list.Count);
            Assert.Contains(list, x => x.FullName == "Ana");
            Assert.Contains(list, x => x.FullName == "Marko");
        }
        [Fact]
        public async Task GetMySeasonAsync_NoLocalUserOrNoSeason_ReturnsNull()
        {
            _users.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<User>());

            var dto1 = await _sut.GetMySeasonAsync(Guid.NewGuid(), CancellationToken.None);
            Assert.Null(dto1);

            var kcId = Guid.NewGuid();
            var u = CreateUser(Guid.NewGuid(), kcId, "U", seasonId: null);
            _users.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<User> { u });

            var dto2 = await _sut.GetMySeasonAsync(kcId, CancellationToken.None);
            Assert.Null(dto2);
        }

        [Fact]
        public async Task GetMySeasonAsync_Valid_ReturnsSeason()
        {
            var kcId = Guid.NewGuid();
            var u = CreateUser(Guid.NewGuid(), kcId, "U", seasonId: 3);
            _users.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<User> { u });

            var s = CreateSeason(3, "S3", DateTime.Today, DateTime.Today.AddDays(1));
            _seasons.Setup(r => r.GetByIdAsync(3, It.IsAny<CancellationToken>(), false))
                .ReturnsAsync(s);

            var dto = await _sut.GetMySeasonAsync(kcId, CancellationToken.None);

            Assert.NotNull(dto);
            Assert.Equal(3, dto!.Id);
            Assert.Equal("S3", dto.Name);
        }

        [Fact]
        public async Task GetMySeasonUsersAsync_NoUserOrNoSeason_ReturnsEmpty()
        {
            _users.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<User>());

            var list1 = await _sut.GetMySeasonUsersAsync(Guid.NewGuid(), CancellationToken.None);
            Assert.Empty(list1);

            var kcId = Guid.NewGuid();
            var u = CreateUser(Guid.NewGuid(), kcId, "U", seasonId: null);
            _users.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<User> { u });

            var list2 = await _sut.GetMySeasonUsersAsync(kcId, CancellationToken.None);
            Assert.Empty(list2);
        }

        [Fact]
        public async Task GetMySeasonUsersAsync_ReturnsOtherUsersFromMySeason()
        {
            var kcId = Guid.NewGuid();
            var me = CreateUser(Guid.NewGuid(), kcId, "Me", seasonId: 5);
            var other1 = CreateUser(Guid.NewGuid(), Guid.NewGuid(), "O1", seasonId: 5);
            var other2 = CreateUser(Guid.NewGuid(), Guid.NewGuid(), "O2", seasonId: 5);

            _users.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<User> { me, other1, other2 });

            var s = CreateSeason(5, "S5", DateTime.Today, DateTime.Today.AddDays(1));
            s.Users.Add(me);
            s.Users.Add(other1);
            s.Users.Add(other2);

            _seasons.Setup(r => r.GetByIdAsync(5, It.IsAny<CancellationToken>(), true))
                .ReturnsAsync(s);

            var list = await _sut.GetMySeasonUsersAsync(kcId, CancellationToken.None);

            Assert.Equal(2, list.Count);
            Assert.DoesNotContain(list, x => x.FullName == "Me");
        }

        [Fact]
        public async Task AddUserByMentorAsync_SeasonNotFound_ReturnsNotFound()
        {
            _seasons.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>(), false))
                .ReturnsAsync((Season?)null);

            var res = await _sut.AddUserByMentorAsync(1, Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

            Assert.Equal(AddUserResult.NotFound, res);
        }

        [Fact]
        public async Task AddUserByMentorAsync_MentorNotSeasonMentor_ReturnsNotFound()
        {
            var mentorKeycloakId = Guid.NewGuid();
            var seasonMentorId = Guid.NewGuid();
            var otherMentorId = Guid.NewGuid(); // ovaj se nađe po KC, ali nije mentor sezone

            var s = CreateSeason(1, "S1", DateTime.Today, DateTime.Today.AddDays(1), mentorId: seasonMentorId);
            _seasons.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>(), false))
                .ReturnsAsync(s);

            var mentor = CreateUser(otherMentorId, mentorKeycloakId);
            _users.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<User> { mentor });

            var res = await _sut.AddUserByMentorAsync(1, Guid.NewGuid(), mentorKeycloakId, CancellationToken.None);

            Assert.Equal(AddUserResult.NotFound, res);
        }

        [Fact]
        public async Task AddUserByMentorAsync_UserNotFound_ReturnsNotFound()
        {
            var mentorKeycloakId = Guid.NewGuid();
            var mentorId = Guid.NewGuid();

            var s = CreateSeason(1, "S1", DateTime.Today, DateTime.Today.AddDays(1), mentorId: mentorId);
            _seasons.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>(), false))
                .ReturnsAsync(s);

            var mentor = CreateUser(mentorId, mentorKeycloakId);
            _users.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<User> { mentor });

            _users.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((User?)null);

            var res = await _sut.AddUserByMentorAsync(1, Guid.NewGuid(), mentorKeycloakId, CancellationToken.None);

            Assert.Equal(AddUserResult.NotFound, res);
        }

        [Fact]
        public async Task AddUserByMentorAsync_UserWithoutKeycloak_ReturnsInvalidRole()
        {
            var mentorKeycloakId = Guid.NewGuid();
            var mentorId = Guid.NewGuid();
            var userId = Guid.NewGuid();

            var s = CreateSeason(1, "S1", DateTime.Today, DateTime.Today.AddDays(1), mentorId: mentorId);
            _seasons.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>(), false))
                .ReturnsAsync(s);

            var mentor = CreateUser(mentorId, mentorKeycloakId);
            _users.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<User> { mentor });

            var user = CreateUser(userId, Guid.Empty);
            _users.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);

            var res = await _sut.AddUserByMentorAsync(1, userId, mentorKeycloakId, CancellationToken.None);

            Assert.Equal(AddUserResult.InvalidRole, res);
        }

        [Fact]
        public async Task AddUserByMentorAsync_UserNotInternAndNotGuest_ReturnsInvalidRole()
        {
            var mentorKeycloakId = Guid.NewGuid();
            var mentorId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var userKcId = Guid.NewGuid();

            var s = CreateSeason(1, "S1", DateTime.Today, DateTime.Today.AddDays(1), mentorId: mentorId);
            _seasons.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>(), false))
                .ReturnsAsync(s);

            var mentor = CreateUser(mentorId, mentorKeycloakId);
            _users.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<User> { mentor });

            var user = CreateUser(userId, userKcId);
            _users.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);

            _kc.Setup(k => k.IsUserInGroupAsync(userKcId, "intern", It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
            _kc.Setup(k => k.IsUserInGroupAsync(userKcId, "guest", It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            var res = await _sut.AddUserByMentorAsync(1, userId, mentorKeycloakId, CancellationToken.None);

            Assert.Equal(AddUserResult.InvalidRole, res);
            _kc.Verify(k => k.ReplaceGroupsWithAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task AddUserByMentorAsync_AlreadyInAnotherSeason_ReturnsAlreadyInAnotherSeason()
        {
            var mentorKeycloakId = Guid.NewGuid();
            var mentorId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var userKcId = Guid.NewGuid();

            var s = CreateSeason(1, "S1", DateTime.Today, DateTime.Today.AddDays(1), mentorId: mentorId);
            _seasons.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>(), false))
                .ReturnsAsync(s);

            var mentor = CreateUser(mentorId, mentorKeycloakId);
            _users.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<User> { mentor });

            var user = CreateUser(userId, userKcId, "Intern", seasonId: 2);
            _users.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);

            _kc.Setup(k => k.IsUserInGroupAsync(userKcId, "intern", It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var res = await _sut.AddUserByMentorAsync(1, userId, mentorKeycloakId, CancellationToken.None);

            Assert.Equal(AddUserResult.AlreadyInAnotherSeason, res);
        }

        [Fact]
        public async Task AddUserByMentorAsync_GuestPromotedToIntern_Success()
        {
            var mentorKeycloakId = Guid.NewGuid();
            var mentorId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var userKcId = Guid.NewGuid();

            var s = CreateSeason(1, "S1", DateTime.Today, DateTime.Today.AddDays(1), mentorId: mentorId);
            _seasons.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>(), false))
                .ReturnsAsync(s);

            var mentor = CreateUser(mentorId, mentorKeycloakId);
            _users.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<User> { mentor });

            var user = CreateUser(userId, userKcId, "Intern", seasonId: null);
            _users.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);

            _kc.Setup(k => k.IsUserInGroupAsync(userKcId, "intern", It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
            _kc.Setup(k => k.IsUserInGroupAsync(userKcId, "guest", It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            _kc.Setup(k => k.ReplaceGroupsWithAsync(userKcId, "intern", It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _users.Setup(r => r.UpdateAsync(user, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _users.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var res = await _sut.AddUserByMentorAsync(1, userId, mentorKeycloakId, CancellationToken.None);

            Assert.Equal(AddUserResult.Ok, res);
            Assert.Equal(1, user.SeasonId);
            _kc.Verify(k => k.ReplaceGroupsWithAsync(userKcId, "intern", It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RemoveUserByMentorAsync_SeasonNotFound_ReturnsFalse()
        {
            _seasons.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>(), false))
                .ReturnsAsync((Season?)null);

            var ok = await _sut.RemoveUserByMentorAsync(1, Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

            Assert.False(ok);
        }

        [Fact]
        public async Task RemoveUserByMentorAsync_MentorNotSeasonMentor_ReturnsFalse()
        {
            var mentorKeycloakId = Guid.NewGuid();
            var seasonMentorId = Guid.NewGuid();
            var otherMentorId = Guid.NewGuid();

            var s = CreateSeason(1, "S1", DateTime.Today, DateTime.Today.AddDays(1), mentorId: seasonMentorId);
            _seasons.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>(), false))
                .ReturnsAsync(s);

            var mentor = CreateUser(otherMentorId, mentorKeycloakId);
            _users.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<User> { mentor });

            var ok = await _sut.RemoveUserByMentorAsync(1, Guid.NewGuid(), mentorKeycloakId, CancellationToken.None);

            Assert.False(ok);
        }

        [Fact]
        public async Task RemoveUserByMentorAsync_UserNotInSeason_ReturnsFalse()
        {
            var mentorKeycloakId = Guid.NewGuid();
            var mentorId = Guid.NewGuid();
            var userId = Guid.NewGuid();

            var s = CreateSeason(1, "S1", DateTime.Today, DateTime.Today.AddDays(1), mentorId: mentorId);
            _seasons.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>(), false))
                .ReturnsAsync(s);

            var mentor = CreateUser(mentorId, mentorKeycloakId);
            _users.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<User> { mentor });

            var user = CreateUser(userId, Guid.NewGuid(), "U", seasonId: 2);
            _users.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);

            var ok = await _sut.RemoveUserByMentorAsync(1, userId, mentorKeycloakId, CancellationToken.None);

            Assert.False(ok);
        }

        [Fact]
        public async Task RemoveUserByMentorAsync_ValidUserWithKeycloak_CallsKeycloakAndClearsSeason()
        {
            var mentorKeycloakId = Guid.NewGuid();
            var mentorId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var userKcId = Guid.NewGuid();

            var s = CreateSeason(1, "S1", DateTime.Today, DateTime.Today.AddDays(1), mentorId: mentorId);
            _seasons.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>(), false))
                .ReturnsAsync(s);

            var mentor = CreateUser(mentorId, mentorKeycloakId);
            _users.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<User> { mentor });

            var user = CreateUser(userId, userKcId, "U", seasonId: 1);
            _users.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);

            _users.Setup(r => r.UpdateAsync(user, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _users.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _kc.Setup(k => k.ReplaceGroupsWithAsync(userKcId, "guest", It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var ok = await _sut.RemoveUserByMentorAsync(1, userId, mentorKeycloakId, CancellationToken.None);

            Assert.True(ok);
            Assert.Null(user.SeasonId);
            _kc.Verify(k => k.ReplaceGroupsWithAsync(userKcId, "guest", It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RemoveUserByMentorAsync_UserWithoutKeycloak_DoesNotCallKeycloak()
        {
            var mentorKeycloakId = Guid.NewGuid();
            var mentorId = Guid.NewGuid();
            var userId = Guid.NewGuid();

            var s = CreateSeason(1, "S1", DateTime.Today, DateTime.Today.AddDays(1), mentorId: mentorId);
            _seasons.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>(), false))
                .ReturnsAsync(s);

            var mentor = CreateUser(mentorId, mentorKeycloakId);
            _users.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<User> { mentor });

            var user = CreateUser(userId, Guid.Empty, "U", seasonId: 1);
            _users.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);

            _users.Setup(r => r.UpdateAsync(user, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _users.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var ok = await _sut.RemoveUserByMentorAsync(1, userId, mentorKeycloakId, CancellationToken.None);

            Assert.True(ok);
            Assert.Null(user.SeasonId);
            _kc.Verify(k => k.ReplaceGroupsWithAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task CreateAsync_CreatedSeasonNotFound_ThrowsNotFound()
        {
            _seasons.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Season>());

            var req = new CreateSeasonRequest(
                "New Season",
                DateTime.Today,
                DateTime.Today.AddDays(1),
                null);

            _seasons.Setup(r => r.AddAsync(It.IsAny<Season>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _seasons.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _seasons.Setup(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>(), false))
                .ReturnsAsync((Season?)null);

            await Assert.ThrowsAsync<NotFoundException>(() =>
                _sut.CreateAsync(req, CancellationToken.None));
        }

        [Fact]
        public async Task UpdateAsync_MentorNotFound_ThrowsNotFound()
        {
            var s = CreateSeason(
                1,
                "S",
                DateTime.Today,
                DateTime.Today.AddDays(5),
                mentorId: null);

            _seasons.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>(), false))
                .ReturnsAsync(s);

            var newMentorId = Guid.NewGuid();
            _users.Setup(r => r.GetByIdAsync(newMentorId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((User?)null);

            var req = new UpdateSeasonRequest(
                Name: null,
                StartDate: null,
                EndDate: null,
                MentorId: newMentorId);

            await Assert.ThrowsAsync<NotFoundException>(() =>
                _sut.UpdateAsync(1, req, CancellationToken.None));
        }

        [Fact]
        public async Task UpdateAsync_MentorWithoutKeycloak_ThrowsValidation()
        {
            var s = CreateSeason(
                1,
                "S",
                DateTime.Today,
                DateTime.Today.AddDays(5),
                mentorId: null);

            _seasons.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>(), false))
                .ReturnsAsync(s);

            var newMentorId = Guid.NewGuid();
            var mentor = CreateUser(newMentorId, Guid.Empty);
            _users.Setup(r => r.GetByIdAsync(newMentorId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(mentor);

            var req = new UpdateSeasonRequest(
                Name: null,
                StartDate: null,
                EndDate: null,
                MentorId: newMentorId);

            await Assert.ThrowsAsync<AppValidationException>(() =>
                _sut.UpdateAsync(1, req, CancellationToken.None));
        }

        [Fact]
        public async Task UpdateAsync_MentorNotInMentorGroup_ThrowsValidation()
        {
            var s = CreateSeason(
                1,
                "S",
                DateTime.Today,
                DateTime.Today.AddDays(5),
                mentorId: null);

            _seasons.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>(), false))
                .ReturnsAsync(s);

            var newMentorId = Guid.NewGuid();
            var kcId = Guid.NewGuid();
            var mentor = CreateUser(newMentorId, kcId);
            _users.Setup(r => r.GetByIdAsync(newMentorId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(mentor);

            _kc.Setup(k => k.IsUserInGroupAsync(kcId, "mentor", It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            var req = new UpdateSeasonRequest(
                Name: null,
                StartDate: null,
                EndDate: null,
                MentorId: newMentorId);

            await Assert.ThrowsAsync<AppValidationException>(() =>
                _sut.UpdateAsync(1, req, CancellationToken.None));
        }


    }
}
