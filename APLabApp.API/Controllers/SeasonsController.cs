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

        public SeasonsController(ISeasonService service) => _service = service;

        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<SeasonDto>>> GetAll(CancellationToken ct)
            => Ok(await _service.GetAllAsync(ct));

        [HttpGet("{id:int}")]
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
            var ok = await _service.AssignMentorAsync(id, body.MentorId, ct);
            return ok ? NoContent() : NotFound();
        }

        [Authorize(Roles = "admin")]
        [HttpPost("{id:int}/users/{userId:guid}")]
        public async Task<IActionResult> AddUser(int id, Guid userId, CancellationToken ct)
        {
            var ok = await _service.AddUserAsync(id, userId, ct);
            return ok ? NoContent() : NotFound();
        }

        [Authorize(Roles = "admin")]
        [HttpDelete("{id:int}/users/{userId:guid}")]
        public async Task<IActionResult> RemoveUser(int id, Guid userId, CancellationToken ct)
        {
            var ok = await _service.RemoveUserAsync(id, userId, ct);
            return ok ? NoContent() : NotFound();
        }

        [HttpGet("{id:int}/users")]
        public async Task<ActionResult<IReadOnlyList<UserDto>>> GetUsers(int id, CancellationToken ct)
            => Ok(await _service.GetUsersAsync(id, ct));
    }

    public sealed record AssignMentorBody(Guid? MentorId);
}
