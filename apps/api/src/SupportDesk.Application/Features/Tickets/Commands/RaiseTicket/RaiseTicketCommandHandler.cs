using Microsoft.Extensions.Logging;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Tickets.Queries.GetTicket;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Repositories;
using SupportDesk.Domain.Services;

namespace SupportDesk.Application.Features.Tickets.Commands.RaiseTicket;

/// <summary>
/// Raises a ticket for a customer, and triages it: priority, due date and owner are decided here
/// by the triage rules, not left for a team lead to work out by hand.
/// </summary>
public sealed class RaiseTicketCommandHandler(
    ITicketRepository tickets,
    ICustomerRepository customers,
    ICategoryRepository categories,
    IAgentRepository agents,
    IUnitOfWork unitOfWork,
    IClock clock,
    TriageService triage,
    GetTicketQueryHandler getTicket,
    ILogger<RaiseTicketCommandHandler> logger)
{
    /// <exception cref="NotFoundException">The customer or the category does not exist.</exception>
    /// <remarks>
    /// A ticket nobody can take is not an error: it is created unassigned, with the reason.
    /// </remarks>
    public async Task<TicketDetailDto> HandleAsync(CreateTicketRequest request, CancellationToken ct)
    {
        var customer = await customers.GetByIdAsync(request.CustomerId, ct)
            ?? throw new NotFoundException("Customer", request.CustomerId);

        var category = await categories.GetByIdAsync(request.CategoryId, ct)
            ?? throw new NotFoundException("Category", request.CategoryId);

        var now = clock.UtcNow;

        var decision = triage.Triage(
            request.RequestedPriority,
            category.ToRequirement(),
            customer.Tier,
            await agents.GetAssignmentCandidatesAsync(ct),
            now);

        var ticket = Ticket.Raise(
            await tickets.NextReferenceAsync(ct),
            request.Title,
            request.Description,
            request.CustomerId,
            request.CategoryId,
            decision.Priority.Priority,
            now);

        ticket.ApplyTriage(
            decision.DueAtUtc,
            decision.Assignment.Agent?.Id,
            decision.Assignment.Agent is null ? decision.Assignment.Reason : null,
            now);

        await tickets.AddAsync(ticket, ct);
        await unitOfWork.SaveChangesAsync(ct);

        logger.LogInformation(
            "Ticket {Reference} raised for customer {CustomerId} in category {CategoryId}: {Priority}, due {DueAtUtc:u}, agent {AgentId}.",
            ticket.Reference, ticket.CustomerId, ticket.CategoryId, ticket.Priority, ticket.DueAtUtc, ticket.AssignedAgentId);

        var created = await getTicket.HandleAsync(ticket.Id, ct);

        created.Triage = new TriageDto(
            decision.Priority.Reason,
            decision.Window.Reason,
            decision.Assignment.Reason);

        return created;
    }
}
