using GarageFlow.Api.Security;
using GarageFlow.Application.Contracts;
using GarageFlow.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GarageFlow.Api.Controllers;

/// <summary>User management. Only an Admin may create or disable accounts.</summary>
[ApiController]
[Authorize(Roles = Roles.Admin)]
[Route("api/users")]
public class UsersController(IUserService users) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<UserResponse>> Create(CreateUserRequest request, CancellationToken ct)
    {
        var created = await users.CreateAsync(request, ct);
        return Created($"/api/users/{created.Id}", created);
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<UserResponse>>> List(CancellationToken ct) =>
        Ok(await users.ListAsync(ct));

    [HttpPost("{id:guid}/deactivate")]
    public async Task<ActionResult<UserResponse>> Deactivate(Guid id, CancellationToken ct) =>
        Ok(await users.DeactivateAsync(id, ct));
}
