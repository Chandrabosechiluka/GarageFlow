using GarageFlow.Application.Contracts;
using GarageFlow.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GarageFlow.Api.Controllers;

/// <summary>Reference data used when booking: services, technicians and bays.</summary>
[ApiController]
[Authorize] // any signed-in user
[Route("api")]
public class CatalogController(ICatalogService catalog) : ControllerBase
{
    [HttpGet("service-types")]
    public async Task<ActionResult<IReadOnlyList<ServiceTypeResponse>>> ServiceTypes(CancellationToken ct) =>
        Ok(await catalog.ListServiceTypesAsync(ct));

    [HttpGet("technicians")]
    public async Task<ActionResult<IReadOnlyList<TechnicianResponse>>> Technicians(CancellationToken ct) =>
        Ok(await catalog.ListTechniciansAsync(ct));

    [HttpGet("bays")]
    public async Task<ActionResult<IReadOnlyList<BayResponse>>> Bays(CancellationToken ct) =>
        Ok(await catalog.ListBaysAsync(ct));
}
