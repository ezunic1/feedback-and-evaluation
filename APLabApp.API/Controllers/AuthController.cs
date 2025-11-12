using APLabApp.BLL.Auth;
using APLabApp.BLL.Errors;
using APLabApp.BLL.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using ValidationException = APLabApp.BLL.Errors.ValidationException;

namespace APLabApp.Api.Controllers
{
    public record RegisterRequest(
        [Required, MinLength(2)] string FullName,
        [Required, EmailAddress] string Email,
        [Required] string Password
    );
    public record LoginRequest(
        [Required] string UsernameOrEmail,
        [Required] string Password,
        string? RedirectUri
    );


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
        public async Task<ActionResult> Login([FromBody] LoginRequest req, CancellationToken ct)
        {
            var raw = req.UsernameOrEmail?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(raw))
                throw new ValidationException("Username or email is required.");

            string identifier = raw;
            if (raw.Contains('@'))
            {
                var emailAttr = new EmailAddressAttribute();
                if (!emailAttr.IsValid(raw))
                    throw new ValidationException("Enter a valid email address.");
                identifier = raw.ToLowerInvariant();
            }

            var password = req.Password?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(password))
                throw new ValidationException("Password is required.");

            try
            {
                var token = await _kc.PasswordTokenAsync(identifier, password, ct);
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
                var msg = ex.Message ?? string.Empty;
                var low = msg.ToLowerInvariant();

                if (low.Contains("invalid_grant") && low.Contains("invalid user credentials"))
                    throw new ValidationException("Wrong email or password.");

                if (low.Contains("user not found"))
                    throw new ValidationException("User does not exist.");

                throw new InvalidOperationException("Login failed.");
            }
        }


        //bez validacije neka stoji za test
        /*[HttpPost("login")]
        public async Task<ActionResult> Login([FromBody] LoginRequest req, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(req.UsernameOrEmail) || string.IsNullOrWhiteSpace(req.Password))
                return BadRequest("Username/email and password are required.");

            try
            {
                var token = await _kc.PasswordTokenAsync(req.UsernameOrEmail, req.Password, ct);
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
                var msg = ex.Message ?? string.Empty;
                var m = msg.ToLowerInvariant();
                if (m.Contains("account is not fully set up") || m.Contains("update_password") || m.Contains("resolve_required_actions"))
                {
                    var url = _kc.BuildBrowserAuthUrl(req.RedirectUri);
                    return StatusCode(428, new
                    {
                        changePasswordUrl = url,
                        message = "You must change your password before the first login."
                    });
                }
                return BadRequest(ex.Message);
            }
        }*/

        [HttpGet("first-login-url")]
        public ActionResult<BrowserUrlResponse> FirstLoginUrl([FromQuery] string? redirectUri = null)
        {
            var url = _kc.BuildBrowserAuthUrl(redirectUri);
            return Ok(new BrowserUrlResponse(url));
        }

       // [Authorize]
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

    //public record RegisterRequest(string FullName, string Email, string Password);
    //public record LoginRequest(string UsernameOrEmail, string Password, string? RedirectUri);
    public record BrowserUrlResponse(string Url);
}
