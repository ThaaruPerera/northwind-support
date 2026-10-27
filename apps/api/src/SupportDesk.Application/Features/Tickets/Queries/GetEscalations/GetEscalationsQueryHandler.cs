using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;

namespace SupportDesk.Application.Features.Tickets.Queries.GetEscalations;

/// <summary>
/// A ticket's escalation history, newest first.
/// </summary>
public sealed class GetEscalationsQueryHandler(ITicketQueries tickets)
{
    /// <exception cref="NotFoundException">The ticket does not exist.</exception>
    public async Task<IReadOnlyList<EscalationDto>> HandleAsync(int ticketId, CancellationToken ct)
    {
        if (!await tickets.ExistsAsync(ticketId, ct))
        {
            throw new NotFoundException("Ticket", ticketId);
        }

        return await tickets.GetEscalationsAsync(ticketId, ct);
    }
}
