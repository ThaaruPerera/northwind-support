using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Application.Features.Auth.Commands.Login;
using SupportDesk.Application.Features.Auth.Queries.GetCurrentAgent;

namespace SupportDesk.Presentation.Controllers;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public sealed class AuthController : ControllerBase
{
    /// <summary>
    /// Signs an agent in. A wrong email, a wrong password and an inactive agent all get the same
    /// <c>401</c> and message.
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public Task<LoginResponse> Login(
        LoginRequest request,
        [FromServices] LoginCommandHandler handler,
        CancellationToken ct) =>
        handler.HandleAsync(request, ct);

    /// <summary>The signed in agent, or <c>401</c>.</summary>
    [HttpGet("me")]
    [Authorize]
    public Task<CurrentAgentDto> Me(
        [FromServices] GetCurrentAgentQueryHandler handler,
        CancellationToken ct) =>
        handler.HandleAsync(ct);
}
