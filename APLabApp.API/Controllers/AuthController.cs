using APLabApp.BLL.Auth;
using APLabApp.BLL.Errors;
using APLabApp.BLL.Users;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace APLabApp.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly IKeycloakAdminService _kc;
        private readonly IValidator<RegisterRequest> _registerValidator;
        private readonly IValidator<LoginRequest> _loginValidator;

        public AuthController(
            IUserService userService,
            IKeycloakAdminService kc,
            IValidator<RegisterRequest> registerValidator,
            IValidator<LoginRequest> loginValidator)
        {
            _userService = userService;
            _kc = kc;
            _registerValidator = registerValidator;
            _loginValidator = loginValidator;
        }

        [HttpPost("register")]
        [AllowAnonymous]
        public async Task<ActionResult<UserDto>> Register([FromBody] RegisterRequest req, CancellationToken ct)
        {
            await _registerValidator.ValidateAndThrowAsync(req, ct);
            var createReq = new CreateUserRequest(req.FullName, req.Email, "Guest self-registration", null, null, req.Password, false);
            var user = await _userService.CreateGuestAsync(createReq, req.Password, ct);
            return CreatedAtAction(nameof(Register), new { id = user.Id }, user);
        }

        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<ActionResult> Login([FromBody] LoginRequest req, CancellationToken ct)
        {
            await _loginValidator.ValidateAndThrowAsync(req, ct);

            var raw = req.UsernameOrEmail!.Trim();
            var identifier = raw.Contains('@') ? raw.ToLowerInvariant() : raw;

            try
            {
                var token = await _kc.PasswordTokenAsync(identifier, req.Password!, ct);
                return Ok(token);
            }
            catch (PasswordChangeRequiredException)
            {
                var url = _kc.BuildBrowserAuthUrl(req.RedirectUri);
                return StatusCode(428, new
                {
                    changePasswordUrl = url,
                    message = "You must change your password before the first login."
                });
            }
            catch (InvalidOperationException ex)
            {
                var low = (ex.Message ?? string.Empty).ToLowerInvariant();
                if (low.Contains("invalid_grant") && low.Contains("invalid user credentials"))
                    throw new AppValidationException("Wrong email or password.");
                if (low.Contains("user not found"))
                    throw new AppValidationException("User does not exist.");
                throw;
            }
        }

        [HttpGet("first-login-url")]
        [AllowAnonymous]
        public ActionResult<BrowserUrlResponse> FirstLoginUrl([FromQuery] string? redirectUri = null)
        {
            var url = _kc.BuildBrowserAuthUrl(redirectUri);
            return Ok(new BrowserUrlResponse(url));
        }

        [HttpPost("sync")]
        [Authorize]
        public async Task<ActionResult<UserDto>> Sync(CancellationToken ct)
        {
            var sub = User.FindFirstValue("sub") ?? User.FindFirstValue("sid");
            if (string.IsNullOrWhiteSpace(sub) || !Guid.TryParse(sub, out var keycloakId))
                throw new AppValidationException("Invalid subject.");

            var email = User.FindFirstValue("email");
            var name = User.FindFirstValue("name") ?? User.FindFirstValue("preferred_username");

            var dto = await _userService.EnsureLocalUserAsync(keycloakId, email, name, ct);
            return Ok(dto);
        }
    }

    public record BrowserUrlResponse(string Url);
}
