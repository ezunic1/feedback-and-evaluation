using APLabApp.BLL.Auth;
using APLabApp.BLL.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading;
using System.Threading.Tasks;

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
            var createReq = new CreateUserRequest(req.FullName, req.Email, "Guest self-registration", null, null);
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
    }

    public record RegisterRequest(string FullName, string Email, string Password);
    public record LoginRequest(string UsernameOrEmail, string Password);
}
