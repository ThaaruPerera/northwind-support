using Microsoft.Extensions.Logging;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Tickets.Queries.GetTicket;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Repositories;
using SupportDesk.Domain.Services;

namespace SupportDesk.Application.Features.Tickets.Commands.EscalateTicket;

/// <summary>
/// Escalates a ticket: one priority up, a new due date counted from now, the owner re-checked,
/// and a record of who asked and why. The rules are the ones raising a ticket uses.
/// </summary>
public sealed class EscalateTicketCommandHandler(
    ITicketRepository tickets,
    ICustomerRepository customers,
    ICategoryRepository categories,
    IAgentRepository agents,
    IUnitOfWork unitOfWork,
    IClock clock,
    ICurrentUser currentUser,
    TriageService triage,
    GetTicketQueryHandler getTicket,
    ITicketQueries ticketQueries,
    ILogger<EscalateTicketCommandHandler> logger)
{
    /// <exception cref="AuthenticationFailedException">No agent is signed in, or the agent is no longer active.</exception>
    /// <exception cref="NotFoundException">The ticket does not exist.</exception>
    /// <exception cref="SupportDesk.Domain.Exceptions.BusinessRuleViolationException">
    /// The ticket is Critical, or already Resolved or Closed.
    /// </exception>
    public async Task<EscalationResultDto> HandleAsync(int id, EscalateTicketRequest request, CancellationToken ct)
    {
        // Who is asking comes first, and from the token: a name in the request would be the caller's word.
        var actor = await SignedInAgentAsync(ct);

        var ticket = await tickets.GetByIdAsync(id, ct) ?? throw new NotFoundException("Ticket", id);

        // Refuse before doing any work, so a ticket that cannot be escalated costs one lookup.
        ticket.EnsureCanBeEscalated();

        var customer = await customers.GetByIdAsync(ticket.CustomerId, ct)
            ?? throw new NotFoundException("Customer", ticket.CustomerId);

        var category = await categories.GetByIdAsync(ticket.CategoryId, ct)
            ?? throw new NotFoundException("Category", ticket.CategoryId);

        var now = clock.UtcNow;

        var decision = triage.Escalate(
            ticket.Priority,
            category.ToRequirement(),
            customer.Tier,
            ticket.AssignedAgentId,
            await agents.GetAssignmentCandidatesAsync(ct),
            now);

        var escalation = ticket.Escalate(
            decision.Priority,
            decision.DueAtUtc,
            decision.Assignment.Agent?.Id,
            decision.Assignment.Agent is null ? decision.Assignment.Reason : null,
            request.Reason,
            ToHistoryName(actor.FullName),
            now);

        // The ticket's change and its history row go to the database in one save, or not at all.
        await tickets.AddEscalationAsync(escalation, ct);
        await unitOfWork.SaveChangesAsync(ct);

        logger.LogInformation(
            "Ticket {Reference} escalated from {From} to {To} by {EscalatedBy}.",
            ticket.Reference, escalation.FromPriority, escalation.ToPriority, escalation.EscalatedBy);

        var updated = await getTicket.HandleAsync(id, ct);
        var recorded = (await ticketQueries.GetEscalationsAsync(id, ct)).First(e => e.Id == escalation.Id);

        return new EscalationResultDto(updated, recorded);
    }

    /// <summary>The agent making the request, looked up again so a deactivated agent cannot act on an old token.</summary>
    private async Task<Domain.Aggregates.Agents.Agent> SignedInAgentAsync(CancellationToken ct)
    {
        var agentId = currentUser.AgentId ?? throw new AuthenticationFailedException(SessionEnded);

        var agent = await agents.GetByIdAsync(agentId, ct);

        return agent is { IsActive: true } ? agent : throw new AuthenticationFailedException(SessionEnded);
    }

    /// <summary>An agent's name can be longer than the history column allows; shorten it rather than fail.</summary>
    private static string ToHistoryName(string fullName) =>
        fullName.Length <= TicketEscalation.EscalatedByMaxLength
            ? fullName
            : fullName[..TicketEscalation.EscalatedByMaxLength];

    private const string SessionEnded = "Your session is no longer valid. Sign in again.";
}
