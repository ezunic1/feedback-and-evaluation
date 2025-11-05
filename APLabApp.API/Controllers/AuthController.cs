using APLabApp.BLL.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace APLabApp.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IUserService _userService;

    public AuthController(IUserService userService)
    {
        _userService = userService;
    }

    [HttpPost("register")]
    public async Task<ActionResult<UserDto>> Register([FromBody] RegisterRequest req, CancellationToken ct)
    {
        var createReq = new CreateUserRequest(
            req.FullName,
            req.Email,
            "Guest self-registration",
            null,
            null
        );

        var user = await _userService.CreateGuestAsync(createReq, req.Password, ct);
        return CreatedAtAction(nameof(Register), new { id = user.Id }, user);
    }
}

public record RegisterRequest(string FullName, string Email, string Password);
