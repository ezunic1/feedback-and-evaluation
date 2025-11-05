using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using APLabApp.BLL.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace APLabApp.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UsersController : ControllerBase
    {
        private readonly IUserService _service;
        public UsersController(IUserService service) => _service = service;

        [Authorize(Roles = "admin,mentor")]
        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<UserDto>>> GetAll(CancellationToken ct)
            => Ok(await _service.GetAllAsync(ct));

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
    }
}
