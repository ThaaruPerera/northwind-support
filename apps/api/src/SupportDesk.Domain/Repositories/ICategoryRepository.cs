using SupportDesk.Domain.Aggregates.Categories;

namespace SupportDesk.Domain.Repositories;

/// <summary>
/// Loads <see cref="Category"/> aggregates.
/// </summary>
public interface ICategoryRepository
{
    /// <summary>The category, or <c>null</c> when there is none with that id.</summary>
    Task<Category?> GetByIdAsync(int id, CancellationToken ct);
}
