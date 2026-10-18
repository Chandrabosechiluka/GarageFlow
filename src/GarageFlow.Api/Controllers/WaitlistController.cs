using GarageFlow.Api.Security;
using GarageFlow.Application.Contracts;
using GarageFlow.Application.Services;
using GarageFlow.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GarageFlow.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/waitlist")]
public class WaitlistController(IWaitlistService waitlist) : ControllerBase
{
    [HttpPost]
    [Authorize(Roles = Roles.AdminOrAdvisor)]
    public async Task<ActionResult<WaitlistEntryResponse>> Join(JoinWaitlistRequest request, CancellationToken ct)
    {
        var created = await waitlist.JoinAsync(request, ct);
        return CreatedAtAction(nameof(List), new { status = created.Status }, created);
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<WaitlistEntryResponse>>> List([FromQuery] WaitlistStatus? status, CancellationToken ct) =>
        Ok(await waitlist.ListAsync(status, ct));

    /// <summary>Takes the offered slot: books an appointment for the customer and returns it.</summary>
    [HttpPost("{id:guid}/accept")]
    [Authorize(Roles = Roles.AdminOrAdvisor)]
    public async Task<ActionResult<AppointmentResponse>> Accept(Guid id, CancellationToken ct) =>
        Ok(await waitlist.AcceptOfferAsync(id, ct));

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Roles = Roles.AdminOrAdvisor)]
    public async Task<ActionResult<WaitlistEntryResponse>> Cancel(Guid id, CancellationToken ct) =>
        Ok(await waitlist.CancelAsync(id, ct));
}
