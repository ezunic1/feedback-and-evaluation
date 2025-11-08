using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using APLabApp.BLL.Users;
using APLabApp.Dal.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace APLabApp.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UsersController : ControllerBase
    {
        private readonly IUserService _service;
        private readonly ISeasonRepository _seasons;

        public UsersController(IUserService service, ISeasonRepository seasons)
        {
            _service = service;
            _seasons = seasons;
        }

        [Authorize(Roles = "admin,mentor")]
        [HttpGet]
        public async Task<ActionResult<PagedResult<UserListItemDto>>> Get([FromQuery] UsersQuery q, CancellationToken ct)
            => Ok(await _service.GetPagedAsync(q, ct));

        [Authorize(Roles = "admin,mentor")]
        [HttpGet("{id:guid}")]
        public async Task<ActionResult<UserDto>> GetById(Guid id, CancellationToken ct)
        {
            var u = await _service.GetByIdAsync(id, ct);
            return u is null ? NotFound() : Ok(u);
        }

        [Authorize(Roles = "admin")]
        [HttpPost]
        public async Task<ActionResult<UserDto>> Create([FromBody] CreateUserRequest req, CancellationToken ct)
        {
            var created = await _service.CreateAsync(req, ct);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }

        [Authorize(Roles = "admin")]
        [HttpPut("{id:guid}")]
        public async Task<ActionResult<UserDto>> Update(Guid id, [FromBody] UpdateUserRequest req, CancellationToken ct)
        {
            var updated = await _service.UpdateAsync(id, req, ct);
            return updated is null ? NotFound() : Ok(updated);
        }

        [Authorize(Roles = "admin")]
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        {
            var ok = await _service.DeleteAsync(id, ct);
            return ok ? NoContent() : NotFound();
        }

        [Authorize]
        [HttpGet("me")]
        public async Task<ActionResult<MeDto>> Me(CancellationToken ct)
        {
            var sub = User.FindFirstValue("sub") ?? User.FindFirstValue("sid");
            if (!Guid.TryParse(sub, out var keycloakId))
                return Unauthorized();

            var u = await _service.GetByKeycloakIdAsync(keycloakId, ct);
            if (u is null)
                return Unauthorized();

            var role = ResolveRole(User);
            var dto = new MeDto { Name = u.FullName, Email = u.Email, Role = role, Description = u.Desc };

            var now = DateTime.UtcNow;
            if (role == "intern")
            {
                var s = await _seasons.GetCurrentForInternAsync(u.Id, now, ct);
                dto.InternSeasonName = s?.Name;
            }
            else if (role == "mentor")
            {
                var s = await _seasons.GetCurrentForMentorAsync(u.Id, now, ct);
                dto.MentorSeasonName = s?.Name;
            }

            return Ok(dto);
        }

        [Authorize]
        [HttpPut("me")]
        public async Task<ActionResult<MeDto>> UpdateMe([FromBody] UpdateMeRequest req, CancellationToken ct)
        {
            var sub = User.FindFirstValue("sub") ?? User.FindFirstValue("sid");
            if (!Guid.TryParse(sub, out var keycloakId))
                return Unauthorized();

            var updated = await _service.UpdateSelfAsync(keycloakId, req.FullName, req.Description, ct);
            if (updated is null) return NotFound();

            var role = ResolveRole(User);
            var dto = new MeDto
            {
                Name = updated.FullName,
                Email = updated.Email,
                Role = role,
                Description = updated.Desc
            };
            return Ok(dto);
        }

        private static string ResolveRole(ClaimsPrincipal principal)
        {
            var allRoles = principal.Claims
                .Where(c => c.Type == "roles" || c.Type == ClaimTypes.Role)
                .Select(c => c.Value.ToLowerInvariant())
                .ToList();

            if (allRoles.Contains("admin")) return "admin";
            if (allRoles.Contains("mentor")) return "mentor";
            if (allRoles.Contains("intern")) return "intern";
            if (allRoles.Contains("guest")) return "guest";
            return "guest";
        }
    }
}
