using System.Security.Claims;
using APLabApp.BLL.Seasons;
using APLabApp.BLL.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace APLabApp.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SeasonsController : ControllerBase
    {
        private readonly ISeasonService _service;
        private readonly IUserService _users;

        public SeasonsController(ISeasonService service, IUserService users)
        {
            _service = service;
            _users = users;
        }

        [HttpGet]
        [Authorize(Roles = "admin,mentor,intern")]
        public async Task<ActionResult<IReadOnlyList<SeasonDto>>> GetAll(CancellationToken ct)
            => Ok(await _service.GetAllAsync(ct));

        [HttpGet("{id:int}")]
        [Authorize(Roles = "admin,mentor,intern")]
        public async Task<ActionResult<SeasonDto>> GetById(int id, CancellationToken ct)
        {
            var s = await _service.GetByIdAsync(id, includeUsers: true, ct);
            return s is null ? NotFound() : Ok(s);
        }

        [Authorize(Roles = "admin")]
        [HttpPost]
        public async Task<ActionResult<SeasonDto>> Create([FromBody] CreateSeasonRequest req, CancellationToken ct)
        {
            var created = await _service.CreateAsync(req, ct);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }

        [Authorize(Roles = "admin")]
        [HttpPut("{id:int}")]
        public async Task<ActionResult<SeasonDto>> Update(int id, [FromBody] UpdateSeasonRequest req, CancellationToken ct)
        {
            var updated = await _service.UpdateAsync(id, req, ct);
            return updated is null ? NotFound() : Ok(updated);
        }

        [Authorize(Roles = "admin")]
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id, CancellationToken ct)
        {
            var ok = await _service.DeleteAsync(id, ct);
            return ok ? NoContent() : NotFound();
        }

        [Authorize(Roles = "admin")]
        [HttpPost("{id:int}/assign-mentor")]
        public async Task<IActionResult> AssignMentor(int id, [FromBody] AssignMentorBody body, CancellationToken ct)
        {
            var res = await _service.AssignMentorAsync(id, body.MentorId, ct);
            return res switch
            {
                AssignMentorResult.Ok => NoContent(),
                AssignMentorResult.NotFound => NotFound(),
                AssignMentorResult.InvalidRole => BadRequest("Selected user is not in 'mentor' group."),
                _ => BadRequest()
            };
        }

        [Authorize(Roles = "admin")]
        [HttpPost("{id:int}/users/{userId:guid}")]
        public async Task<IActionResult> AddUser(int id, Guid userId, CancellationToken ct)
        {
            var res = await _service.AddUserAsync(id, userId, ct);
            return res switch
            {
                AddUserResult.Ok => NoContent(),
                AddUserResult.NotFound => NotFound(),
                AddUserResult.InvalidRole => BadRequest("User is not in 'intern' group."),
                AddUserResult.AlreadyInAnotherSeason => BadRequest("User is already assigned to another season."),
                _ => BadRequest()
            };
        }

        [Authorize(Roles = "admin")]
        [HttpDelete("{id:int}/users/{userId:guid}")]
        public async Task<IActionResult> RemoveUser(int id, Guid userId, CancellationToken ct)
        {
            var ok = await _service.RemoveUserAsync(id, userId, ct);
            return ok ? NoContent() : NotFound();
        }

        [Authorize]
        [HttpGet("{id:int}/users")]
        public async Task<ActionResult<IReadOnlyList<UserDto>>> GetUsers(int id, CancellationToken ct)
        {
            var roles = User.Claims
                .Where(c => c.Type == ClaimTypes.Role || c.Type == "roles")
                .Select(c => c.Value)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var isAdmin = roles.Contains("admin");
            var isMentor = roles.Contains("mentor");
            var isIntern = roles.Contains("intern");

            if (isAdmin || isMentor || isIntern)
            {
                var listAll = await _service.GetUsersAsync(id, ct);
                return Ok(listAll);
            }

            return Forbid();
        }

        [Authorize(Roles = "intern")]
        [HttpGet("me")]
        public async Task<ActionResult<SeasonDto>> GetMySeason(CancellationToken ct)
        {
            var sub = User.FindFirstValue("sub");
            if (!Guid.TryParse(sub, out var kcId) || kcId == Guid.Empty)
                return Forbid();

            var dto = await _service.GetMySeasonAsync(kcId, ct);
            if (dto is null) return NoContent();
            return Ok(dto);
        }

        [Authorize(Roles = "intern")]
        [HttpGet("me/users")]
        public async Task<ActionResult<IReadOnlyList<UserDto>>> GetMySeasonUsers(CancellationToken ct)
        {
            var sub = User.FindFirstValue("sub");
            if (!Guid.TryParse(sub, out var kcId) || kcId == Guid.Empty)
                return Forbid();

            var list = await _service.GetMySeasonUsersAsync(kcId, ct);
            return Ok(list);
        }

        [Authorize(Roles = "mentor")]
        [HttpPost("{id:int}/users/{userId:guid}/by-mentor")]
        public async Task<IActionResult> MentorAddUser(int id, Guid userId, CancellationToken ct)
        {
            var sub = User.FindFirstValue("sub");
            if (!Guid.TryParse(sub, out var mentorKcId) || mentorKcId == Guid.Empty)
                return Forbid();

            var res = await _service.AddUserByMentorAsync(id, userId, mentorKcId, ct);
            return res switch
            {
                AddUserResult.Ok => NoContent(),
                AddUserResult.NotFound => NotFound(),
                AddUserResult.InvalidRole => BadRequest("User is not in 'intern' group."),
                AddUserResult.AlreadyInAnotherSeason => BadRequest("User is already assigned to another season."),
                _ => BadRequest()
            };
        }

        [Authorize(Roles = "mentor")]
        [HttpDelete("{id:int}/users/{userId:guid}/by-mentor")]
        public async Task<IActionResult> MentorRemoveUser(int id, Guid userId, CancellationToken ct)
        {
            var sub = User.FindFirstValue("sub");
            if (!Guid.TryParse(sub, out var mentorKcId) || mentorKcId == Guid.Empty)
                return Forbid();

            var ok = await _service.RemoveUserByMentorAsync(id, userId, mentorKcId, ct);
            return ok ? NoContent() : NotFound();
        }
    }

    public sealed record AssignMentorBody(Guid? MentorId);
}
