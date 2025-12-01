using APLabApp.BLL.Auth;
using APLabApp.BLL.Errors;
using APLabApp.BLL.Users;
using APLabApp.Dal.Entities;
using APLabApp.Dal.Repositories;
using APLabApp.Tests.Feedbacks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.Configuration;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace APLabApp.Tests.Users
{
    public class UserServiceTests
    {
        private readonly Mock<IUserRepository> _users;
        private readonly Mock<ISeasonRepository> _seasons;
        private readonly Mock<IKeycloakAdminService> _kc;
        private readonly Mock<IConfiguration> _cfg;
        private readonly UserService _sut;

        public UserServiceTests()
        {
            _users = new Mock<IUserRepository>();
            _seasons = new Mock<ISeasonRepository>();
            _kc = new Mock<IKeycloakAdminService>();
            _cfg = new Mock<IConfiguration>();
            _cfg.Setup(c => c["Keycloak:UseEmailAsUsername"]).Returns((string?)null);

            _sut = new UserService(_users.Object, _seasons.Object, _kc.Object, _cfg.Object);
        }

        private static User CreateUser(Guid id, Guid keycloakId, string email, string fullName, int? seasonId = null, DateTime? created = null)
            => new()
            {
                Id = id,
                KeycloakId = keycloakId,
                Email = email,
                FullName = fullName,
                SeasonId = seasonId,
                CreatedAtUtc = created ?? DateTime.UtcNow
            };

        [Fact]
        public async Task GetPagedAsync_NoFilters_ReturnsPagedUsersWithRoles()
        {
            var u1Id = Guid.NewGuid();
            var u2Id = Guid.NewGuid();
            var k1 = Guid.NewGuid();
            var k2 = Guid.NewGuid();

            var data = new List<User>
            {
                CreateUser(u1Id, k1, "a@test.local", "User A", created: new DateTime(2024,1,1)),
                CreateUser(u2Id, k2, "b@test.local", "User B", created: new DateTime(2024,1,2))
            }.AsUserAsyncQueryable();

            _users.Setup(r => r.Query()).Returns(data);

            _kc.Setup(k => k.GetRealmRolesBulkAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<Guid, string[]>
                {
                    { k1, new[] { "intern" } },
                    { k2, new[] { "mentor" } }
                });

            _kc.Setup(k => k.GetGroupsBulkAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<Guid, string[]>());

            var query = new UsersQuery
            {
                Page = 1,
                PageSize = 10
            };

            var result = await _sut.GetPagedAsync(query, CancellationToken.None);

            Assert.Equal(2, result.Items.Count);
            Assert.Equal("mentor", result.Items[0].Role);
            Assert.Equal("intern", result.Items[1].Role);
            Assert.Equal(2, result.TotalCount);
        }

        [Fact]
        public async Task GetPagedAsync_RoleFilterEmpty_UsesFalsePredicateAndReturnsEmpty()
        {
            var data = new List<User>
            {
                CreateUser(Guid.NewGuid(), Guid.NewGuid(), "a@test.local", "User A"),
                CreateUser(Guid.NewGuid(), Guid.NewGuid(), "b@test.local", "User B")
            }.AsUserAsyncQueryable();

            _users.Setup(r => r.Query()).Returns(data);

            _kc.Setup(k => k.GetUserIdsInRealmRoleAsync("mentor", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new HashSet<Guid>());
            _kc.Setup(k => k.GetUserIdsInGroupAsync("mentor", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new HashSet<Guid>());

            var query = new UsersQuery
            {
                Page = 1,
                PageSize = 10,
                Role = "mentor"
            };

            var result = await _sut.GetPagedAsync(query, CancellationToken.None);

            Assert.Empty(result.Items);
            Assert.Equal(0, result.TotalCount);
        }

        [Fact]
        public async Task GetPagedAsync_SearchDateSeasonAndRoleFilter()
        {
            var seasonId = 5;
            var k1 = Guid.NewGuid();
            var k2 = Guid.NewGuid();

            var baseTime = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            var data = new List<User>
            {
                CreateUser(Guid.NewGuid(), k1, "ana@example.com", "Ana Intern", seasonId, baseTime.AddDays(1)),
                CreateUser(Guid.NewGuid(), k2, "ivan@example.com", "Ivan Intern", seasonId, baseTime.AddDays(2)),
                CreateUser(Guid.NewGuid(), Guid.NewGuid(), "marko@example.com", "Marko", null, baseTime.AddDays(-1))
            }.AsUserAsyncQueryable();

            _users.Setup(r => r.Query()).Returns(data);

            _kc.Setup(k => k.GetUserIdsInRealmRoleAsync("intern", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new HashSet<Guid> { k1, k2 });
            _kc.Setup(k => k.GetUserIdsInGroupAsync("intern", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new HashSet<Guid>());

            _kc.Setup(k => k.GetRealmRolesBulkAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<Guid, string[]>());
            _kc.Setup(k => k.GetGroupsBulkAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<Guid, string[]>());

            var query = new UsersQuery
            {
                Page = 1,
                PageSize = 10,
                Q = "ana",
                From = baseTime,
                To = baseTime.AddDays(10),
                SeasonId = seasonId,
                Role = "intern",
                SortBy = "name",
                SortDir = "asc"
            };

            var result = await _sut.GetPagedAsync(query, CancellationToken.None);

            Assert.Single(result.Items);
            Assert.Equal("Ana Intern", result.Items[0].FullName);
        }

        [Fact]
        public async Task GetAllAsync_ReturnsMappedUsers()
        {
            var u = CreateUser(Guid.NewGuid(), Guid.NewGuid(), "a@test.local", "User A");
            _users.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<User> { u });

            var result = await _sut.GetAllAsync(CancellationToken.None);

            Assert.Single(result);
            Assert.Equal(u.Id, result[0].Id);
        }

        [Fact]
        public async Task GetByIdAsync_UserNotFound_ReturnsNull()
        {
            _users.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((User?)null);

            var dto = await _sut.GetByIdAsync(Guid.NewGuid(), CancellationToken.None);

            Assert.Null(dto);
        }

        [Fact]
        public async Task GetByIdAsync_WithSeasonAndKeycloak_ReturnsRoleAndSeason()
        {
            var id = Guid.NewGuid();
            var kcId = Guid.NewGuid();
            var user = CreateUser(id, kcId, "a@test.local", "User A", seasonId: 3);

            _users.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);

            _seasons.Setup(s => s.GetByIdAsync(3, It.IsAny<CancellationToken>(), It.IsAny<bool>()))
                .ReturnsAsync(new Season { Id = 3, Name = "Season 3" });

            _kc.Setup(k => k.GetRealmRolesBulkAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<Guid, string[]> { { kcId, new[] { "admin", "intern" } } });
            _kc.Setup(k => k.GetGroupsBulkAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<Guid, string[]>());

            var dto = await _sut.GetByIdAsync(id, CancellationToken.None);

            Assert.NotNull(dto);
            Assert.Equal("admin", dto!.RoleName);
            Assert.Equal("Season 3", dto.SeasonName);
        }

        [Fact]
        public async Task GetByIdAsync_KeycloakEmpty_ReturnsGuestRole()
        {
            var id = Guid.NewGuid();
            var user = CreateUser(id, Guid.Empty, "a@test.local", "User A", seasonId: 3);

            _users.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);

            _seasons.Setup(s => s.GetByIdAsync(3, It.IsAny<CancellationToken>(), It.IsAny<bool>()))
                .ReturnsAsync(new Season { Id = 3, Name = "Season 3" });

            var dto = await _sut.GetByIdAsync(id, CancellationToken.None);

            Assert.NotNull(dto);
            Assert.Equal("guest", dto!.RoleName);
            Assert.Equal("Season 3", dto.SeasonName);
            _kc.Verify(k => k.GetRealmRolesBulkAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task GetByKeycloakIdAsync_UserNotFound_ReturnsNull()
        {
            _users.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<User>());

            var dto = await _sut.GetByKeycloakIdAsync(Guid.NewGuid(), CancellationToken.None);

            Assert.Null(dto);
        }

        [Fact]
        public async Task GetByKeycloakIdAsync_ReturnsUserWithRoleAndSeason()
        {
            var kcId = Guid.NewGuid();
            var user = CreateUser(Guid.NewGuid(), kcId, "a@test.local", "User A", seasonId: 10);

            _users.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<User> { user });
            _seasons.Setup(s => s.GetByIdAsync(10, It.IsAny<CancellationToken>(), It.IsAny<bool>()))
                .ReturnsAsync(new Season { Id = 10, Name = "S10" });

            _kc.Setup(k => k.GetRealmRolesBulkAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<Guid, string[]> { { kcId, new[] { "intern" } } });
            _kc.Setup(k => k.GetGroupsBulkAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<Guid, string[]>());

            var dto = await _sut.GetByKeycloakIdAsync(kcId, CancellationToken.None);

            Assert.NotNull(dto);
            Assert.Equal("intern", dto!.RoleName);
            Assert.Equal("S10", dto.SeasonName);
        }

        [Fact]
        public async Task CreateAsync_ThrowsWhenFullNameMissing()
        {
            var req = new CreateUserRequest(
                "",
                "a@test.local",
                null,
                null,
                "guest",
                null,
                false);

            await Assert.ThrowsAsync<ArgumentException>(() =>
                _sut.CreateAsync(req, CancellationToken.None));
        }

        [Fact]
        public async Task CreateAsync_ThrowsWhenEmailMissing()
        {
            var req = new CreateUserRequest(
                "User A",
                "",
                null,
                null,
                "guest",
                null,
                false);

            await Assert.ThrowsAsync<ArgumentException>(() =>
                _sut.CreateAsync(req, CancellationToken.None));
        }

        [Fact]
        public async Task CreateAsync_ThrowsWhenNonInternHasSeason()
        {
            var req = new CreateUserRequest(
                "User A",
                "a@test.local",
                null,
                5,
                "mentor",
                null,
                false);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _sut.CreateAsync(req, CancellationToken.None));
        }

        [Fact]
        public async Task CreateAsync_ThrowsWhenKeycloakReturnsNull()
        {
            var req = new CreateUserRequest(
                "User A",
                "a@test.local",
                null,
                null,
                "guest",
                null,
                false);

            _kc.Setup(k => k.CreateUserAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<bool>()))
                .ReturnsAsync((Guid?)null);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _sut.CreateAsync(req, CancellationToken.None));
        }

        [Fact]
        public async Task CreateAsync_UsesEmailAsUsernameWhenFlagTrue()
        {
            var cfgTrue = new Mock<IConfiguration>();
            cfgTrue.Setup(c => c["Keycloak:UseEmailAsUsername"]).Returns("true");

            var sutLocal = new UserService(_users.Object, _seasons.Object, _kc.Object, cfgTrue.Object);

            var req = new CreateUserRequest(
                "User A",
                "UPPER@Example.COM",
                null,
                null,
                "guest",
                null,
                false);

            string? capturedUsername = null;

            _kc.Setup(k => k.CreateUserAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<bool>()))
                .Callback<string, string, string, string, string, CancellationToken, bool>((u, e, f, p, r, ct, fp) =>
                {
                    capturedUsername = u;
                })
                .ReturnsAsync(Guid.NewGuid());

            _users.Setup(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _users.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            await sutLocal.CreateAsync(req, CancellationToken.None);

            Assert.Equal("upper@example.com", capturedUsername);
        }

        [Fact]
        public async Task UpdateAsync_UserNotFound_ReturnsNull()
        {
            _users.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((User?)null);

            var req = new UpdateUserRequest(null, null, null, null, null);

            var result = await _sut.UpdateAsync(Guid.NewGuid(), req, CancellationToken.None);

            Assert.Null(result);
        }
        [Fact]
        public async Task UpdateAsync_UpdatesRoleSeasonAndNameAndKeycloak()
        {
            var id = Guid.NewGuid();
            var kcId = Guid.NewGuid();
            var user = new User
            {
                Id = id,
                KeycloakId = kcId,
                FullName = "Old Name",
                Email = "a@test.local",
                SeasonId = 4
            };

            _users.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);
            _users.Setup(r => r.UpdateAsync(user, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _users.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            string? first = null;
            string? last = null;

            _kc.Setup(k => k.ReplaceGroupsWithAsync(kcId, "mentor", It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _kc.Setup(k => k.UpdateUserProfileAsync(
                    kcId,
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<IDictionary<string, string>>(),
                    It.IsAny<CancellationToken>()))
               .Callback<Guid, string, string, IDictionary<string, string>, CancellationToken>((_, f, l, _, __) =>
               {
                   first = f;
                   last = l;
               })
               .ReturnsAsync(true);

            var req = new UpdateUserRequest("  New Name ", null, null, null, "mentor");

            var result = await _sut.UpdateAsync(id, req, CancellationToken.None);

            Assert.NotNull(result);
            // ime NIJE trimovano u servisu
            Assert.Equal("  New Name ", user.FullName);
            // ali SplitName je radio ispravno
            Assert.Equal("New", first);
            Assert.Equal("Name", last);
            Assert.Null(user.SeasonId);

            _kc.Verify(k => k.ReplaceGroupsWithAsync(kcId, "mentor", It.IsAny<CancellationToken>()), Times.Once);
            _kc.Verify(k => k.UpdateUserProfileAsync(
                kcId,
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IDictionary<string, string>>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }



        [Fact]
        public async Task UpdateSelfAsync_UserNotFound_ReturnsNull()
        {
            _users.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<User>());

            var result = await _sut.UpdateSelfAsync(Guid.NewGuid(), "New Name", "desc", CancellationToken.None);

            Assert.Null(result);
        }
        [Fact]
        public async Task UpdateSelfAsync_SingleWordName_SplitsCorrectly()
        {
            var kcId = Guid.NewGuid();
            var user = new User
            {
                Id = Guid.NewGuid(),
                KeycloakId = kcId,
                Email = "a@test.local",
                FullName = "Old Name"
            };

            _users.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<User> { user });
            _users.Setup(r => r.UpdateAsync(user, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _users.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            string? first = null;
            string? last = null;

            _kc.Setup(k => k.UpdateUserProfileAsync(
                kcId,
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IDictionary<string, string>>(),
                It.IsAny<CancellationToken>()))
           .Callback<Guid, string, string, IDictionary<string, string>, CancellationToken>((_, f, l, _, __) =>
           {
               first = f;
               last = l;
           })
           .ReturnsAsync(true);



            var dto = await _sut.UpdateSelfAsync(kcId, "  Prince  ", "desc", CancellationToken.None);

            Assert.NotNull(dto);
            Assert.Equal("Prince", user.FullName);
            Assert.Equal("Prince", first);
            Assert.Equal(string.Empty, last);
        }

        [Fact]
        public async Task UpdateSelfAsync_MultiWordName_SplitsFirstAndRest()
        {
            var kcId = Guid.NewGuid();
            var user = new User
            {
                Id = Guid.NewGuid(),
                KeycloakId = kcId,
                Email = "a@test.local",
                FullName = "Old Name"
            };

            _users.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<User> { user });
            _users.Setup(r => r.UpdateAsync(user, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _users.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            string? first = null;
            string? last = null;

            _kc.Setup(k => k.UpdateUserProfileAsync(
                kcId,
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IDictionary<string, string>>(),
                It.IsAny<CancellationToken>()))
           .Callback<Guid, string, string, IDictionary<string, string>, CancellationToken>((_, f, l, _, __) =>
           {
               first = f;
               last = l;
           })
           .ReturnsAsync(true);



            var dto = await _sut.UpdateSelfAsync(kcId, " John Mark Doe ", "about", CancellationToken.None);

            Assert.NotNull(dto);
            Assert.Equal("John Mark Doe", user.FullName);
            Assert.Equal("John", first);
            Assert.Equal("Mark Doe", last);
        }

        [Fact]
        public async Task UpdateAsync_NameEmpty_DoesNotChangeNameOrCallKeycloak()
        {
            var id = Guid.NewGuid();
            var kcId = Guid.NewGuid();
            var user = new User
            {
                Id = id,
                KeycloakId = kcId,
                FullName = "Old Name",
                Email = "a@test.local",
                SeasonId = 10
            };

            _users.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);
            _users.Setup(r => r.UpdateAsync(user, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _users.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _kc.Setup(k => k.ReplaceGroupsWithAsync(kcId, "guest", It.IsAny<CancellationToken>()))
               .Returns(Task.CompletedTask);

            var req = new UpdateUserRequest(
                FullName: string.Empty, 
                Email: null,
                Desc: null,
                SeasonId: null,
                RoleName: "guest");

            var dto = await _sut.UpdateAsync(id, req, CancellationToken.None);

            Assert.NotNull(dto);
            
            Assert.Equal("Old Name", user.FullName);
          
            Assert.Null(user.SeasonId);

            _kc.Verify(k => k.ReplaceGroupsWithAsync(kcId, "guest", It.IsAny<CancellationToken>()), Times.Once);

            _kc.Verify(k => k.UpdateUserProfileAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IDictionary<string, string>>(),
                It.IsAny<CancellationToken>()), Times.Never);
        }




        [Fact]
        public async Task GetPagedAsync_SortByNameDesc_UsesNameDescendingOrder()
        {
            var k1 = Guid.NewGuid();
            var k2 = Guid.NewGuid();
            var users = new List<User>
            {
                new User { Id = Guid.NewGuid(), KeycloakId = k1, FullName = "Ana",  Email = "ana@test.com",  CreatedAtUtc = DateTime.UtcNow },
                new User { Id = Guid.NewGuid(), KeycloakId = k2, FullName = "Zoran",Email = "zoran@test.com",CreatedAtUtc = DateTime.UtcNow }
            }.AsTestAsyncQueryable();

            _users.Setup(r => r.Query()).Returns(users);

            _kc.Setup(k => k.GetRealmRolesBulkAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<Guid, string[]>());
            _kc.Setup(k => k.GetGroupsBulkAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<Guid, string[]>());

            var query = new UsersQuery
            {
                Page = 1,
                PageSize = 10,
                SortBy = "name",
                SortDir = "DESC"
            };

            var result = await _sut.GetPagedAsync(query, CancellationToken.None);

            Assert.Equal(2, result.Items.Count);
            Assert.Equal("Zoran", result.Items[0].FullName); // descending by name
        }

        [Fact]
        public async Task GetPagedAsync_SortByEmailAsc_UsesEmailAscendingOrder()
        {
            var k1 = Guid.NewGuid();
            var k2 = Guid.NewGuid();
            var users = new List<User>
            {
                new User { Id = Guid.NewGuid(), KeycloakId = k1, FullName = "User1", Email = "z@test.com", CreatedAtUtc = DateTime.UtcNow },
                new User { Id = Guid.NewGuid(), KeycloakId = k2, FullName = "User2", Email = "a@test.com", CreatedAtUtc = DateTime.UtcNow }
            }.AsTestAsyncQueryable();

            _users.Setup(r => r.Query()).Returns(users);

            _kc.Setup(k => k.GetRealmRolesBulkAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<Guid, string[]>());
            _kc.Setup(k => k.GetGroupsBulkAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<Guid, string[]>());

            var query = new UsersQuery
            {
                Page = 1,
                PageSize = 10,
                SortBy = "email",
                SortDir = "asc"
            };

            var result = await _sut.GetPagedAsync(query, CancellationToken.None);

            Assert.Equal(2, result.Items.Count);
            Assert.Equal("a@test.com", result.Items[0].Email); // ascending by email
        }

        [Fact]
        public async Task GetPagedAsync_SortByEmailDesc_UsesEmailDescendingOrder()
        {
            var k1 = Guid.NewGuid();
            var k2 = Guid.NewGuid();
            var users = new List<User>
            {
                new User { Id = Guid.NewGuid(), KeycloakId = k1, FullName = "User1", Email = "a@test.com", CreatedAtUtc = DateTime.UtcNow },
                new User { Id = Guid.NewGuid(), KeycloakId = k2, FullName = "User2", Email = "z@test.com", CreatedAtUtc = DateTime.UtcNow }
            }.AsTestAsyncQueryable();

            _users.Setup(r => r.Query()).Returns(users);

            _kc.Setup(k => k.GetRealmRolesBulkAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<Guid, string[]>());
            _kc.Setup(k => k.GetGroupsBulkAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<Guid, string[]>());

            var query = new UsersQuery
            {
                Page = 1,
                PageSize = 10,
                SortBy = "email",
                SortDir = "DESC"
            };

            var result = await _sut.GetPagedAsync(query, CancellationToken.None);

            Assert.Equal(2, result.Items.Count);
            Assert.Equal("z@test.com", result.Items[0].Email); // descending by email
        }

        [Fact]
        public async Task GetPagedAsync_SortDirAscWithoutSortBy_UsesCreatedAtAscending()
        {
            var k1 = Guid.NewGuid();
            var k2 = Guid.NewGuid();
            var older = DateTime.UtcNow.AddDays(-1);
            var newer = DateTime.UtcNow;

            var users = new List<User>
            {
                new User { Id = Guid.NewGuid(), KeycloakId = k1, FullName = "User1", Email = "u1@test.com", CreatedAtUtc = newer },
                new User { Id = Guid.NewGuid(), KeycloakId = k2, FullName = "User2", Email = "u2@test.com", CreatedAtUtc = older }
            }.AsTestAsyncQueryable();

            _users.Setup(r => r.Query()).Returns(users);

            _kc.Setup(k => k.GetRealmRolesBulkAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<Guid, string[]>());
            _kc.Setup(k => k.GetGroupsBulkAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<Guid, string[]>());

            var query = new UsersQuery
            {
                Page = 1,
                PageSize = 10,
                SortBy = null,
                SortDir = "asc"
            };

            var result = await _sut.GetPagedAsync(query, CancellationToken.None);

            Assert.Equal(2, result.Items.Count);
            Assert.True(result.Items[0].CreatedAt <= result.Items[1].CreatedAt); // ascending by CreatedAtUtc
        }

        [Fact]
        public async Task UpdateSelfAsync_UpdatesNameAndDescriptionAndKeycloak()
        {
            var kcId = Guid.NewGuid();
            var user = CreateUser(Guid.NewGuid(), kcId, "a@test.local", "Old Name");

            _users.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<User> { user });

            _users.Setup(r => r.UpdateAsync(user, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _users.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _kc.Setup(k => k.UpdateUserProfileAsync(kcId, "New", "Name", null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);


            var result = await _sut.UpdateSelfAsync(kcId, " New Name ", "about me", CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal("New Name", user.FullName);
            Assert.Equal("about me", user.Desc);
            _kc.Verify(k => k.UpdateUserProfileAsync(kcId, "New", "Name", null, It.IsAny<CancellationToken>()), Times.Once);
        }


        [Fact]
        public async Task DeleteAsync_UserNotFound_ReturnsFalse()
        {
            _users.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((User?)null);

            var ok = await _sut.DeleteAsync(Guid.NewGuid(), CancellationToken.None);

            Assert.False(ok);
        }

        [Fact]
        public async Task DeleteAsync_DeletesLocalAndKeycloak()
        {
            var id = Guid.NewGuid();
            var kcId = Guid.NewGuid();
            var user = CreateUser(id, kcId, "a@test.local", "User A");

            _users.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);

            _kc.Setup(k => k.DeleteUserAsync(kcId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            _users.Setup(r => r.DeleteAsync(user, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _users.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            var ok = await _sut.DeleteAsync(id, CancellationToken.None);

            Assert.True(ok);
            _kc.Verify(k => k.DeleteUserAsync(kcId, It.IsAny<CancellationToken>()), Times.Once);
            _users.Verify(r => r.DeleteAsync(user, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task DeleteAsync_KeycloakDeleteFails_Throws()
        {
            var id = Guid.NewGuid();
            var kcId = Guid.NewGuid();
            var user = CreateUser(id, kcId, "a@test.local", "User A");

            _users.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);

            _kc.Setup(k => k.DeleteUserAsync(kcId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _sut.DeleteAsync(id, CancellationToken.None));
        }

        [Fact]
        public async Task ChangePasswordAsync_UserNotFoundOrNoKeycloak_ReturnsFalse()
        {
            _users.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((User?)null);

            var ok1 = await _sut.ChangePasswordAsync(Guid.NewGuid(), "x", null, CancellationToken.None);
            Assert.False(ok1);

            var user = CreateUser(Guid.NewGuid(), Guid.Empty, "", "User");
            _users.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);

            var ok2 = await _sut.ChangePasswordAsync(user.Id, "x", null, CancellationToken.None);
            Assert.False(ok2);
        }

        [Fact]
        public async Task ChangePasswordAsync_WithCurrentPasswordVerificationFails_ReturnsFalse()
        {
            var id = Guid.NewGuid();
            var kcId = Guid.NewGuid();
            var user = CreateUser(id, kcId, "a@test.local", "User");

            _users.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);

            _kc.Setup(k => k.VerifyUserPasswordAsync("a@test.local", "old", It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            var ok = await _sut.ChangePasswordAsync(id, "new", "old", CancellationToken.None);

            Assert.False(ok);
            _kc.Verify(k => k.ResetPasswordAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task ChangePasswordAsync_WithCurrentPasswordVerificationOk_ResetsPassword()
        {
            var id = Guid.NewGuid();
            var kcId = Guid.NewGuid();
            var user = CreateUser(id, kcId, "a@test.local", "User");

            _users.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);

            _kc.Setup(k => k.VerifyUserPasswordAsync("a@test.local", "old", It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            _kc.Setup(k => k.ResetPasswordAsync(kcId, "new", It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var ok = await _sut.ChangePasswordAsync(id, "new", "old", CancellationToken.None);

            Assert.True(ok);
        }

        [Fact]
        public async Task ChangePasswordAsync_WithoutCurrentPassword_CallsResetDirectly()
        {
            var id = Guid.NewGuid();
            var kcId = Guid.NewGuid();
            var user = CreateUser(id, kcId, "a@test.local", "User");

            _users.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);

            _kc.Setup(k => k.ResetPasswordAsync(kcId, "new", It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var ok = await _sut.ChangePasswordAsync(id, "new", null, CancellationToken.None);

            Assert.True(ok);
            _kc.Verify(k => k.VerifyUserPasswordAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task CreateGuestAsync_EmailExists_ThrowsConflict()
        {
            var existing = new List<User>
            {
                CreateUser(Guid.NewGuid(), Guid.NewGuid(), "existing@test.local", "User")
            }.AsUserAsyncQueryable();

            _users.Setup(r => r.Query()).Returns(existing);

            var req = new CreateUserRequest(
                "User A",
                "existing@test.local",
                null,
                null,
                null,
                null,
                false);

            await Assert.ThrowsAsync<ConflictException>(() =>
                _sut.CreateGuestAsync(req, "Password1!", CancellationToken.None));
        }

        [Fact]
        public async Task CreateGuestAsync_KeycloakThrowsConflict_Rethrows()
        {
            var data = new List<User>().AsUserAsyncQueryable();
            _users.Setup(r => r.Query()).Returns(data);

            var req = new CreateUserRequest(
                "User A",
                "a@test.local",
                null,
                null,
                null,
                null,
                false);

            _kc.Setup(k => k.CreateUserAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<bool>()))
                .ThrowsAsync(new ConflictException("kc"));

            await Assert.ThrowsAsync<ConflictException>(() =>
                _sut.CreateGuestAsync(req, "Password1!", CancellationToken.None));
        }

        [Fact]
        public async Task CreateGuestAsync_KeycloakReturnsNull_ThrowsConflict()
        {
            var data = new List<User>().AsUserAsyncQueryable();
            _users.Setup(r => r.Query()).Returns(data);

            var req = new CreateUserRequest(
                "User A",
                "a@test.local",
                null,
                null,
                null,
                null,
                false);

            _kc.Setup(k => k.CreateUserAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<bool>()))
                .ReturnsAsync((Guid?)null);

            await Assert.ThrowsAsync<ConflictException>(() =>
                _sut.CreateGuestAsync(req, "Password1!", CancellationToken.None));
        }

        [Fact]
        public async Task CreateGuestAsync_Success_CreatesUser()
        {
            var data = new List<User>().AsUserAsyncQueryable();
            _users.Setup(r => r.Query()).Returns(data);

            var req = new CreateUserRequest(
                "User A",
                "a@test.local",
                null,
                null,
                null,
                null,
                false);

            var kcId = Guid.NewGuid();
            Guid? capturedKcId = null;

            _kc.Setup(k => k.CreateUserAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<bool>()))
                .ReturnsAsync(kcId);

            _users.Setup(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
                .Callback<User, CancellationToken>((u, _) => capturedKcId = u.KeycloakId)
                .Returns(Task.CompletedTask);
            _users.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            var dto = await _sut.CreateGuestAsync(req, "Password1!", CancellationToken.None);

            Assert.NotEqual(Guid.Empty, dto.Id);
            Assert.Equal(kcId, capturedKcId);
        }

        [Fact]
        public async Task EnsureLocalUserAsync_Existing_ReturnsMapped()
        {
            var kcId = Guid.NewGuid();
            var user = CreateUser(Guid.NewGuid(), kcId, "a@test.local", "User A");

            _users.Setup(r => r.ExistsByKeycloakIdAsync(kcId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            _users.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<User> { user });

            var dto = await _sut.EnsureLocalUserAsync(kcId, "a@test.local", "User A", CancellationToken.None);

            Assert.Equal(user.Id, dto.Id);
            _users.Verify(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task EnsureLocalUserAsync_New_AddsAndReturns()
        {
            var kcId = Guid.NewGuid();

            _users.Setup(r => r.ExistsByKeycloakIdAsync(kcId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            User? added = null;
            _users.Setup(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
                .Callback<User, CancellationToken>((u, _) => added = u)
                .Returns(Task.CompletedTask);
            _users.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            var dto = await _sut.EnsureLocalUserAsync(kcId, "a@test.local", "User A", CancellationToken.None);

            Assert.NotNull(added);
            Assert.Equal(kcId, added!.KeycloakId);
            Assert.Equal(added.Id, dto.Id);
        }
        [Fact]
        public async Task GetByIdAsync_OnlyGuestGroup_ReturnsGuestRole()
        {
            var id = Guid.NewGuid();
            var kcId = Guid.NewGuid();
            var user = CreateUser(id, kcId, "a@test.local", "User A", seasonId: 3);

            _users.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);

            _seasons.Setup(s => s.GetByIdAsync(3, It.IsAny<CancellationToken>(), It.IsAny<bool>()))
                .ReturnsAsync(new Season { Id = 3, Name = "Season 3" });

            _kc.Setup(k => k.GetRealmRolesBulkAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<Guid, string[]>());
            _kc.Setup(k => k.GetGroupsBulkAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<Guid, string[]>
                {
                    { kcId, new[] { "guest" } }
                });

            var dto = await _sut.GetByIdAsync(id, CancellationToken.None);

            Assert.NotNull(dto);
            Assert.Equal("guest", dto!.RoleName);
            Assert.Equal("Season 3", dto.SeasonName);
        }

        [Fact]
        public async Task GetByIdAsync_NoRolesOrGroups_ReturnsGuestRole()
        {
            var id = Guid.NewGuid();
            var kcId = Guid.NewGuid();
            var user = CreateUser(id, kcId, "a@test.local", "User A", seasonId: 3);

            _users.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);

            _seasons.Setup(s => s.GetByIdAsync(3, It.IsAny<CancellationToken>(), It.IsAny<bool>()))
                .ReturnsAsync(new Season { Id = 3, Name = "Season 3" });

            _kc.Setup(k => k.GetRealmRolesBulkAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<Guid, string[]>());
            _kc.Setup(k => k.GetGroupsBulkAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<Guid, string[]>());

            var dto = await _sut.GetByIdAsync(id, CancellationToken.None);

            Assert.NotNull(dto);
            Assert.Equal("guest", dto!.RoleName);
            Assert.Equal("Season 3", dto.SeasonName);
        }


    }

    internal static class UserAsyncQueryableExtensions
    {
        public static IQueryable<T> AsUserAsyncQueryable<T>(this IEnumerable<T> source)
            => new UserAsyncEnumerable<T>(source);
    }

    internal class UserAsyncEnumerable<T> : EnumerableQuery<T>, IAsyncEnumerable<T>, IQueryable<T>
    {
        public UserAsyncEnumerable(IEnumerable<T> enumerable) : base(enumerable) { }
        public UserAsyncEnumerable(Expression expression) : base(expression) { }

        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
            => new UserAsyncEnumerator<T>(this.AsEnumerable().GetEnumerator());

        IQueryProvider IQueryable.Provider => new UserAsyncQueryProvider<T>(this);
    }

    internal class UserAsyncEnumerator<T> : IAsyncEnumerator<T>
    {
        private readonly IEnumerator<T> _inner;

        public UserAsyncEnumerator(IEnumerator<T> inner)
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
            => new(_inner.MoveNext());
    }

    internal class UserAsyncQueryProvider<TEntity> : IAsyncQueryProvider
    {
        private readonly IQueryProvider _inner;

        public UserAsyncQueryProvider(IQueryProvider inner)
        {
            _inner = inner;
        }

        public IQueryable CreateQuery(Expression expression)
            => new UserAsyncEnumerable<TEntity>(expression);

        public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
            => new UserAsyncEnumerable<TElement>(expression);

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
}
