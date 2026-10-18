using GarageFlow.Api.Security;
using GarageFlow.Application.Common;
using GarageFlow.Application.Contracts;
using GarageFlow.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GarageFlow.Api.Controllers;

[ApiController]
[Authorize(Roles = Roles.Admin)]
[Route("api/audit")]
public class AuditController(IAuditService audit) : ControllerBase
{
    /// <summary>The audit trail, newest first. Filter by entity (for example Appointment), its id, or who did it.</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<AuditLogResponse>>> List(
        [FromQuery] string? entityName,
        [FromQuery] Guid? entityId,
        [FromQuery] string? performedBy,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default) =>
        Ok(await audit.ListAsync(entityName, entityId, performedBy, new PageRequest(page, pageSize), ct));
}
