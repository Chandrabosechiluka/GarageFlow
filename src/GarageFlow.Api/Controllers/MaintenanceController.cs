using GarageFlow.Api.Security;
using GarageFlow.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GarageFlow.Api.Controllers;

/// <summary>Runs the background jobs right now, so you can see them work without waiting for the timer.</summary>
[ApiController]
[Authorize(Roles = Roles.Admin)]
[Route("api/maintenance")]
public class MaintenanceController(IMaintenanceService maintenance) : ControllerBase
{
    [HttpPost("run")]
    public async Task<ActionResult<MaintenanceReport>> Run(CancellationToken ct)
    {
        var noShows = await maintenance.MarkNoShowsAsync(ct);
        var reminders = await maintenance.SendRemindersAsync(ct);
        var notified = await maintenance.NotifyOffersAsync(ct);
        var expired = await maintenance.ExpireOffersAsync(ct);
        return Ok(new MaintenanceReport(noShows, reminders, notified, expired));
    }
}
