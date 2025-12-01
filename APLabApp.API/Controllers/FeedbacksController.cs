using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using APLabApp.BLL.Feedbacks;
using APLabApp.BLL.Users;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace APLabApp.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FeedbacksController : ControllerBase
    {
        private readonly IFeedbackService _service;
        private readonly IUserService _users;
        private readonly IValidator<CreateInternFeedbackRequest> _internValidator;
        private readonly IValidator<CreateMentorFeedbackRequest> _mentorValidator;

        public FeedbacksController(
            IFeedbackService service,
            IUserService users,
            IValidator<CreateInternFeedbackRequest> internValidator,
            IValidator<CreateMentorFeedbackRequest> mentorValidator)
        {
            _service = service;
            _users = users;
            _internValidator = internValidator;
            _mentorValidator = mentorValidator;
        }

        [Authorize(Roles = "intern,mentor")]
        [HttpGet("me")]
        public async Task<ActionResult<IReadOnlyList<FeedbackDto>>> GetMyFeedbacks([FromQuery] int page = 1, CancellationToken ct = default)
        {
            var sub = User.FindFirstValue("sub") ?? User.FindFirstValue("sid");
            if (!Guid.TryParse(sub, out var keycloakId))
                return Unauthorized();

            var u = await _users.GetByKeycloakIdAsync(keycloakId, ct);
            if (u is null)
                return Unauthorized();

            var role = ResolveRole(User);
            if (role == "intern")
            {
                var list = await _service.GetForInternAsync(u.Id, page, 10, ct);
                return Ok(list);
            }

            if (role == "mentor")
            {
                var list = await _service.GetForMentorAsync(u.Id, page, 500, ct);
                return Ok(list);
            }

            return Forbid();
        }

        [Authorize(Roles = "admin")]
        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<FeedbackDto>>> GetForAdmin([FromQuery] int? seasonId, [FromQuery] int page = 1, CancellationToken ct = default)
        {
            var list = await _service.GetForAdminAsync(seasonId, page, 500, ct);
            return Ok(list);
        }

        [Authorize(Roles = "intern")]
        [HttpGet("me/received/mentor")]
        public async Task<ActionResult<IReadOnlyList<FeedbackDto>>> GetReceivedFromMentor([FromQuery] int page = 1, CancellationToken ct = default)
        {
            var sub = User.FindFirstValue("sub") ?? User.FindFirstValue("sid");
            if (!Guid.TryParse(sub, out var keycloakId))
                return Unauthorized();

            var u = await _users.GetByKeycloakIdAsync(keycloakId, ct);
            if (u is null)
                return Unauthorized();

            var list = await _service.GetReceivedFromMentorAsync(u.Id, ct);
            return Ok(list);
        }

        [Authorize(Roles = "intern")]
        [HttpGet("me/received/interns")]
        public async Task<ActionResult<IReadOnlyList<FeedbackDto>>> GetReceivedFromInterns([FromQuery] int page = 1, CancellationToken ct = default)
        {
            var sub = User.FindFirstValue("sub") ?? User.FindFirstValue("sid");
            if (!Guid.TryParse(sub, out var keycloakId))
                return Unauthorized();

            var u = await _users.GetByKeycloakIdAsync(keycloakId, ct);
            if (u is null)
                return Unauthorized();

            var list = await _service.GetReceivedFromInternsAsync(u.Id, ct);
            return Ok(list);
        }

        [Authorize(Roles = "intern,mentor")]
        [HttpGet("me/sent")]
        public async Task<ActionResult<IReadOnlyList<FeedbackDto>>> GetSentByMe([FromQuery] int page = 1, CancellationToken ct = default)
        {
            var sub = User.FindFirstValue("sub") ?? User.FindFirstValue("sid");
            if (!Guid.TryParse(sub, out var keycloakId))
                return Unauthorized();

            var u = await _users.GetByKeycloakIdAsync(keycloakId, ct);
            if (u is null)
                return Unauthorized();

            var list = await _service.GetSentByMeAsync(u.Id, ct);
            return Ok(list);
        }

        [Authorize(Roles = "intern")]
        [HttpPost("intern")]
        public async Task<ActionResult<FeedbackDto>> CreateAsIntern([FromBody] CreateInternFeedbackRequest req, CancellationToken ct)
        {
            await _internValidator.ValidateAndThrowAsync(req, ct);
            var sub = User.FindFirstValue("sub") ?? User.FindFirstValue("sid");
            if (!Guid.TryParse(sub, out var keycloakId))
                return Unauthorized();

            var u = await _users.GetByKeycloakIdAsync(keycloakId, ct);
            if (u is null)
                return Unauthorized();

            var created = await _service.CreateInternFeedbackAsync(u.Id, req, ct);
            return Ok(created);
        }

        [Authorize(Roles = "mentor")]
        [HttpPost("mentor")]
        public async Task<ActionResult<FeedbackDto>> CreateAsMentor([FromBody] CreateMentorFeedbackRequest req, CancellationToken ct)
        {
            await _mentorValidator.ValidateAndThrowAsync(req, ct);
            var sub = User.FindFirstValue("sub") ?? User.FindFirstValue("sid");
            if (!Guid.TryParse(sub, out var keycloakId))
                return Unauthorized();

            var u = await _users.GetByKeycloakIdAsync(keycloakId, ct);
            if (u is null)
                return Unauthorized();

            var created = await _service.CreateMentorFeedbackAsync(u.Id, req, ct);
            return Ok(created);
        }

        [Authorize(Roles = "admin")]
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id, CancellationToken ct)
        {
            await _service.DeleteAsync(id, ct);
            return NoContent();
        }

        [Authorize(Roles = "mentor")]
        [HttpGet("mentor/averages")]
        public async Task<ActionResult<MentorMonthlyAveragesPageDto>> GetMentorAverages(
            [FromQuery] int seasonId,
            [FromQuery] int monthIndex,
            [FromQuery] string? sortBy = "grade",
            [FromQuery] string? sortDir = "desc",
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            CancellationToken ct = default)
        {
            var sub = User.FindFirstValue("sub") ?? User.FindFirstValue("sid");
            if (!Guid.TryParse(sub, out var keycloakId))
                return Unauthorized();

            var me = await _users.GetByKeycloakIdAsync(keycloakId, ct);
            if (me is null)
                return Unauthorized();

            var dto = await _service.GetMentorMonthlyAveragesPagedAsync(me.Id, seasonId, monthIndex, sortBy, sortDir, page, pageSize, ct);
            return Ok(dto);
        }

        [Authorize(Roles = "admin,mentor")]
        [HttpGet("search")]
        public async Task<ActionResult<APLabApp.BLL.PagedResult<FeedbackDto>>> Search(
            [FromQuery] int seasonId,
            [FromQuery] string? type = "all",
            [FromQuery] string? sortDir = "desc",
            [FromQuery] int? monthIndex = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            CancellationToken ct = default)
        {
            var role = ResolveRole(User);
            if (role == "admin")
            {
                var resA = await _service.SearchForAdminAsync(seasonId, type, sortDir, monthIndex, page, pageSize, ct);
                return Ok(resA);
            }

            if (role == "mentor")
            {
                var sub = User.FindFirstValue("sub") ?? User.FindFirstValue("sid");
                if (!Guid.TryParse(sub, out var keycloakId))
                    return Unauthorized();

                var me = await _users.GetByKeycloakIdAsync(keycloakId, ct);
                if (me is null)
                    return Unauthorized();

                var resM = await _service.SearchForMentorAsync(me.Id, seasonId, type, sortDir, monthIndex, page, pageSize, ct);
                return Ok(resM);
            }

            return Forbid();
        }

        [Authorize(Roles = "admin,mentor")]
        [HttpGet("season/{seasonId:int}/months")]
        public async Task<ActionResult<IReadOnlyList<MonthSpanDto>>> GetSeasonMonths(int seasonId, CancellationToken ct = default)
        {
            var spans = await _service.GetSeasonMonthSpansAsync(seasonId, ct);
            return Ok(spans);
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
