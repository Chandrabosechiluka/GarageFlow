using GarageFlow.Application.Abstractions;
using GarageFlow.Application.Common;
using GarageFlow.Application.Contracts;
using GarageFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GarageFlow.Application.Services;

public interface ICustomerService
{
    Task<CustomerResponse> CreateAsync(CreateCustomerRequest request, CancellationToken ct = default);
    Task<CustomerResponse> GetAsync(Guid id, CancellationToken ct = default);
    Task<PagedResult<CustomerResponse>> ListAsync(string? search, PageRequest? page = null, CancellationToken ct = default);
    Task<VehicleResponse> AddVehicleAsync(Guid customerId, AddVehicleRequest request, CancellationToken ct = default);
}

public class CustomerService(IGarageFlowDbContext db) : ICustomerService
{
    public async Task<CustomerResponse> CreateAsync(CreateCustomerRequest request, CancellationToken ct = default)
    {
        // The Customer constructor lowercases the email, so compare in lowercase too.
        var email = request.Email.Trim().ToLowerInvariant();
        if (await db.Customers.AnyAsync(c => c.Email == email, ct))
            throw new ConflictException($"A customer with email {email} already exists.");

        var customer = new Customer(request.FullName, request.Email, request.Phone);
        db.Customers.Add(customer);
        await db.SaveChangesAsync(ct);

        return customer.ToResponse();
    }

    public async Task<CustomerResponse> GetAsync(Guid id, CancellationToken ct = default)
    {
        var customer = await db.Customers.AsNoTracking()
            .Include(c => c.Vehicles)
            .FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new NotFoundException(nameof(Customer), id);

        return customer.ToResponse();
    }

    public async Task<PagedResult<CustomerResponse>> ListAsync(string? search, PageRequest? page = null, CancellationToken ct = default)
    {
        IQueryable<Customer> query = db.Customers.AsNoTracking().Include(c => c.Vehicles);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(c => c.FullName.ToLower().Contains(term) || c.Email.Contains(term));
        }

        return await query.OrderBy(c => c.FullName).ThenBy(c => c.Id)
            .ToPagedAsync(page, c => c.ToResponse(), ct);
    }

    public async Task<VehicleResponse> AddVehicleAsync(Guid customerId, AddVehicleRequest request, CancellationToken ct = default)
    {
        // Load the customer WITH its vehicles: the domain rule "no duplicate registration" needs to see them.
        var customer = await db.Customers
            .Include(c => c.Vehicles)
            .FirstOrDefaultAsync(c => c.Id == customerId, ct)
            ?? throw new NotFoundException(nameof(Customer), customerId);

        var vehicle = customer.AddVehicle(request.Make, request.Model, request.Year, request.RegistrationNumber);
        db.Vehicles.Add(vehicle);
        await db.SaveChangesAsync(ct);

        return vehicle.ToResponse();
    }
}
