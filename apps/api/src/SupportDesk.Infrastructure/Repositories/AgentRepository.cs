using Microsoft.EntityFrameworkCore;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Repositories;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.Infrastructure.Repositories;

public sealed class AgentRepository(SupportDbContext db) : IAgentRepository
{
    /// <summary>Loads the whole aggregate, specializations included.</summary>
    public Task<Agent?> GetByIdAsync(int id, CancellationToken ct) =>
        db.Set<Agent>()
            .Include(a => a.Specializations)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

    public Task<Agent?> GetByEmailAsync(string email, CancellationToken ct)
    {
        var normalised = email.Trim().ToLowerInvariant();

        // Lowered on both sides, so it is case insensitive on any database and not only where the
        // column's collation happens to be. With a handful of agents the index is not missed; a large
        // directory would store the lowered email in a column of its own.
        return db.Set<Agent>()
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Email.ToLower() == normalised, ct);
    }

    public async Task<IReadOnlyList<AgentCandidate>> GetAssignmentCandidatesAsync(CancellationToken ct) =>
        await db.Set<Agent>()
            .AsNoTracking()
            .OrderBy(a => a.Id)
            .Select(a => new AgentCandidate(
                a.Id,
                a.FullName,
                a.IsActive,
                a.MaxOpenTickets,
                db.Set<Ticket>().Where(Ticket.IsOpenExpression).Count(t => t.AssignedAgentId == a.Id),
                a.Specializations.Select(s => s.CategoryId).ToList()))
            .ToListAsync(ct);
}
