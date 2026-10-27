using Microsoft.EntityFrameworkCore;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Repositories;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.Infrastructure.Repositories;

/// <summary>
/// Loads <see cref="Category"/> aggregates. Categories are read-only reference data to the
/// commands, so they are not tracked.
/// </summary>
public sealed class CategoryRepository(SupportDbContext db) : ICategoryRepository
{
    public Task<Category?> GetByIdAsync(int id, CancellationToken ct) =>
        db.Set<Category>().AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct);
}
