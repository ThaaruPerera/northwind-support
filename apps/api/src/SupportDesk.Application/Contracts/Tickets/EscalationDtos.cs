using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Application.Contracts.Tickets;

/// <summary>One row of a ticket's escalation history: what changed, who asked, and why.</summary>
public sealed record EscalationDto(
    int Id,
    int TicketId,
    TicketPriority FromPriority,
    TicketPriority ToPriority,
    AgentSummaryDto? FromAgent,
    AgentSummaryDto? ToAgent,
    DateTime? FromDueAtUtc,
    DateTime ToDueAtUtc,
    string Reason,
    string EscalatedBy,
    DateTime EscalatedAtUtc);

/// <summary>The ticket after it was escalated, and the history row that recorded it.</summary>
public sealed record EscalationResultDto(TicketDetailDto Ticket, EscalationDto Escalation);
