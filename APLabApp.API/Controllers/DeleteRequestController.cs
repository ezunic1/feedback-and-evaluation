using APLabApp.Bll.Services;
using APLabApp.BLL.DeleteRequests;
using APLabApp.BLL.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace APLabApp.Api.Controllers
{
    [ApiController]
    [Route("api/delete-requests")]
    public class DeleteRequestsController : ControllerBase
    {
        private readonly IDeleteRequestService _svc;
        private readonly IUserService _users;

        public DeleteRequestsController(IDeleteRequestService svc, IUserService users)
        {
            _svc = svc;
            _users = users;
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateDeleteRequestBody body, CancellationToken ct)
        {
            if (body is null || body.FeedbackId <= 0 || string.IsNullOrWhiteSpace(body.Reason))
                return BadRequest("feedbackId i reason su obavezni.");

            var sub = User.FindFirstValue("sub") ?? User.FindFirstValue("sid");
            if (!Guid.TryParse(sub, out var keycloakId))
                return Unauthorized();

            var me = await _users.GetByKeycloakIdAsync(keycloakId, ct);
            if (me is null)
                return Unauthorized();

            var id = await _svc.CreateAsync(
                new CreateDeleteRequestDto
                {
                    FeedbackId = body.FeedbackId,
                    SenderUserId = me.Id,
                    Reason = body.Reason
                }, ct);

            return CreatedAtAction(nameof(GetById), new { id }, new { id });
        }

        [Authorize(Roles = "admin")]
        [HttpGet]
        public async Task<ActionResult<List<DeleteRequestListItemDto>>> GetAll(CancellationToken ct)
            => Ok(await _svc.GetAllAsync(ct));

        [Authorize(Roles = "admin")]
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id, CancellationToken ct)
        {
            var all = await _svc.GetAllAsync(ct);
            var item = all.FirstOrDefault(x => x.Id == id);
            return item is null ? NotFound() : Ok(item);
        }

        [Authorize(Roles = "admin")]
        [HttpPost("{id:int}/approve")]
        public async Task<IActionResult> Approve(int id, CancellationToken ct)
        {
            await _svc.ApproveAsync(id, ct);
            return NoContent();
        }

        [Authorize(Roles = "admin")]
        [HttpPost("{id:int}/reject")]
        public async Task<IActionResult> Reject(int id, CancellationToken ct)
        {
            await _svc.RejectAsync(id, ct);
            return NoContent();
        }
    }

    public sealed class CreateDeleteRequestBody
    {
        public int FeedbackId { get; set; }
        public string Reason { get; set; } = string.Empty;
    }
}
