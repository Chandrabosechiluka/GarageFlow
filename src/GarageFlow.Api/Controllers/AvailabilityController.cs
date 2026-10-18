using System.ComponentModel.DataAnnotations;
using GarageFlow.Application.Contracts;
using GarageFlow.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GarageFlow.Api.Controllers;

[ApiController]
[Authorize] // any signed-in user
[Route("api/availability")]
public class AvailabilityController(IAvailabilityService availability) : ControllerBase
{
    /// <summary>
    /// Free start times for a service on one local day (the date is in the shop's time zone).
    /// Optionally limit to one technician. Book one of the returned startUtc values with POST /api/appointments.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<AvailabilityResponse>> Get(
        [FromQuery, Required] Guid? serviceTypeId,
        [FromQuery, Required] DateOnly? date,
        [FromQuery] Guid? shopId,
        [FromQuery] Guid? technicianId,
        CancellationToken ct) =>
        Ok(await availability.GetAsync(serviceTypeId!.Value, date!.Value, shopId, technicianId, ct));
}
