using GarageFlow.Api.Security;
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

    // Setting up the shop is an Admin job. Everyone else can only read these lists.
    [HttpPost("technicians")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<TechnicianResponse>> CreateTechnician(CreateTechnicianRequest request, CancellationToken ct)
    {
        var created = await catalog.CreateTechnicianAsync(request, ct);
        return Created($"/api/technicians/{created.Id}", created);
    }

    [HttpPost("bays")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<BayResponse>> CreateBay(CreateBayRequest request, CancellationToken ct)
    {
        var created = await catalog.CreateBayAsync(request, ct);
        return Created($"/api/bays/{created.Id}", created);
    }

    [HttpPost("service-types")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<ServiceTypeResponse>> CreateServiceType(CreateServiceTypeRequest request, CancellationToken ct)
    {
        var created = await catalog.CreateServiceTypeAsync(request, ct);
        return Created($"/api/service-types/{created.Id}", created);
    }
}
