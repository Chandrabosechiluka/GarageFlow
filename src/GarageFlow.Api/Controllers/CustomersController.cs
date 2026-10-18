using GarageFlow.Application.Contracts;
using GarageFlow.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace GarageFlow.Api.Controllers;

[ApiController]
[Route("api/customers")]
public class CustomersController(ICustomerService customers) : ControllerBase
{
    // Action methods are named without the "Async" suffix on purpose: ASP.NET strips that suffix from
    // action names, which would break CreatedAtAction(nameof(...)).

    [HttpPost]
    public async Task<ActionResult<CustomerResponse>> Create(CreateCustomerRequest request, CancellationToken ct)
    {
        var created = await customers.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CustomerResponse>> Get(Guid id, CancellationToken ct) =>
        Ok(await customers.GetAsync(id, ct));

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CustomerResponse>>> List([FromQuery] string? search, CancellationToken ct) =>
        Ok(await customers.ListAsync(search, ct));

    [HttpPost("{id:guid}/vehicles")]
    public async Task<ActionResult<VehicleResponse>> AddVehicle(Guid id, AddVehicleRequest request, CancellationToken ct)
    {
        var vehicle = await customers.AddVehicleAsync(id, request, ct);
        return CreatedAtAction(nameof(Get), new { id }, vehicle);
    }
}
