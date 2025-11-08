using APLabApp.BLL.Auth;
using APLabApp.BLL.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using System;

namespace APLabApp.Api.Controllers
{
    [ApiController]
    [AllowAnonymous]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly IKeycloakAdminService _kc;

        public AuthController(IUserService userService, IKeycloakAdminService kc)
        {
            _userService = userService;
            _kc = kc;
        }

        [HttpPost("register")]
        public async Task<ActionResult<UserDto>> Register([FromBody] RegisterRequest req, CancellationToken ct)
        {
            var createReq = new CreateUserRequest(
                req.FullName,
                req.Email,
                "Guest self-registration",
                null,
                null,
                req.Password,
                false
            );
            var user = await _userService.CreateGuestAsync(createReq, req.Password, ct);
            return CreatedAtAction(nameof(Register), new { id = user.Id }, user);
        }

        [HttpPost("login")]
        public async Task<ActionResult<TokenResponse>> Login([FromBody] LoginRequest req, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(req.UsernameOrEmail) || string.IsNullOrWhiteSpace(req.Password))
                return BadRequest("Username/email and password are required.");

            var token = await _kc.PasswordTokenAsync(req.UsernameOrEmail, req.Password, ct);
            return Ok(token);
        }

        [Authorize]
        [HttpPost("sync")]
        public async Task<ActionResult<UserDto>> Sync(CancellationToken ct)
        {
            var sub = User.FindFirstValue("sub") ?? User.FindFirstValue("sid");
            if (string.IsNullOrWhiteSpace(sub) || !Guid.TryParse(sub, out var keycloakId))
                return BadRequest("Invalid subject");

            var email = User.FindFirstValue("email");
            var name = User.FindFirstValue("name") ?? User.FindFirstValue("preferred_username");

            var dto = await _userService.EnsureLocalUserAsync(keycloakId, email, name, ct);
            return Ok(dto);
        }
    }

    public record RegisterRequest(string FullName, string Email, string Password);
    public record LoginRequest(string UsernameOrEmail, string Password);
}
