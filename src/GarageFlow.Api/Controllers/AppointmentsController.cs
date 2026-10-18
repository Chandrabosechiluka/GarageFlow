using GarageFlow.Api.Security;
using GarageFlow.Application.Contracts;
using GarageFlow.Application.Services;
using GarageFlow.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GarageFlow.Api.Controllers;

[ApiController]
[Authorize] // reading: any signed-in user; changes: see the attributes below
[Route("api/appointments")]
public class AppointmentsController(IAppointmentService appointments) : ControllerBase
{
    [HttpPost]
    [Authorize(Roles = Roles.AdminOrAdvisor)]
    public async Task<ActionResult<AppointmentResponse>> Book(BookAppointmentRequest request, CancellationToken ct)
    {
        var created = await appointments.BookAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AppointmentResponse>> Get(Guid id, CancellationToken ct) =>
        Ok(await appointments.GetAsync(id, ct));

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AppointmentResponse>>> List(
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] AppointmentStatus? status,
        CancellationToken ct) =>
        Ok(await appointments.ListAsync(fromUtc, toUtc, status, ct));

    // Status changes are POSTs to action-style URLs: they are commands, not edits of a field.

    [HttpPost("{id:guid}/check-in")]
    [Authorize(Roles = Roles.AdminOrAdvisor)]
    public async Task<ActionResult<AppointmentResponse>> CheckIn(Guid id, CancellationToken ct) =>
        Ok(await appointments.CheckInAsync(id, ct));

    [HttpPost("{id:guid}/start-inspection")]
    [Authorize(Roles = Roles.AdminOrTechnician)]
    public async Task<ActionResult<AppointmentResponse>> StartInspection(Guid id, CancellationToken ct) =>
        Ok(await appointments.StartInspectionAsync(id, ct));

    [HttpPost("{id:guid}/inspection-items")]
    [Authorize(Roles = Roles.AdminOrTechnician)]
    public async Task<ActionResult<AppointmentResponse>> AddInspectionItem(Guid id, AddInspectionItemRequest request, CancellationToken ct) =>
        Ok(await appointments.AddInspectionItemAsync(id, request, ct));

    [HttpPost("{id:guid}/complete")]
    [Authorize(Roles = Roles.AdminOrTechnician)]
    public async Task<ActionResult<AppointmentResponse>> Complete(Guid id, CancellationToken ct) =>
        Ok(await appointments.CompleteAsync(id, ct));

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Roles = Roles.AdminOrAdvisor)]
    public async Task<ActionResult<AppointmentResponse>> Cancel(Guid id, CancelAppointmentRequest request, CancellationToken ct) =>
        Ok(await appointments.CancelAsync(id, request, ct));

    [HttpPost("{id:guid}/no-show")]
    [Authorize(Roles = Roles.AdminOrAdvisor)]
    public async Task<ActionResult<AppointmentResponse>> NoShow(Guid id, CancellationToken ct) =>
        Ok(await appointments.MarkNoShowAsync(id, ct));
}
