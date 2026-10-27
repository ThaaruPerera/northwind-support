using Microsoft.EntityFrameworkCore;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Repositories;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.Infrastructure.Repositories;

/// <summary>
/// Loads <see cref="Customer"/> aggregates. Commands only read a customer (to know their tier),
/// so they are not tracked.
/// </summary>
public sealed class CustomerRepository(SupportDbContext db) : ICustomerRepository
{
    public Task<Customer?> GetByIdAsync(int id, CancellationToken ct) =>
        db.Set<Customer>().AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct);
}
