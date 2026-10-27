using SupportDesk.Domain.Aggregates.Customers;

namespace SupportDesk.Domain.Repositories;

/// <summary>
/// Loads <see cref="Customer"/> aggregates.
/// </summary>
public interface ICustomerRepository
{
    /// <summary>The customer, or <c>null</c> when there is none with that id.</summary>
    Task<Customer?> GetByIdAsync(int id, CancellationToken ct);
}
