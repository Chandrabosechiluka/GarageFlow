using GarageFlow.Application.Contracts;
using GarageFlow.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GarageFlow.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(IAuthService auth) : ControllerBase
{
    /// <summary>Exchange email and password for a JWT. This is the only endpoint (besides health) that needs no token.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken ct) =>
        Ok(await auth.LoginAsync(request, ct));

    /// <summary>Shows what the API understood from your token. Handy for learning and debugging.</summary>
    [HttpGet("me")]
    [Authorize]
    public ActionResult<object> Me() => Ok(new
    {
        Name = User.FindFirst("name")?.Value,
        Email = User.FindFirst("email")?.Value,
        Role = User.FindFirst("role")?.Value,
        UserId = User.FindFirst("sub")?.Value
    });
}
