using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Application.Contracts.Tickets;

/// <summary>
/// A single ticket, with the description and contact details the detail screen shows.
/// </summary>
public sealed class TicketDetailDto
{
    public int Id { get; set; }

    public string Reference { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public TicketStatus Status { get; set; }

    public TicketPriority Priority { get; set; }

    public CustomerContactDto Customer { get; set; } = null!;

    public CategoryDto Category { get; set; } = null!;

    public AgentSummaryDto? AssignedAgent { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public DateTime? DueAtUtc { get; set; }

    public DateTime? ResolvedAtUtc { get; set; }

    /// <summary>Derived from the dates above; never stored.</summary>
    public SlaStatus SlaStatus { get; set; }

    /// <summary>Why nobody owns the ticket. Null while it has an owner.</summary>
    public string? UnassignedReason { get; set; }

    /// <summary>
    /// The reasons behind the decisions made when the ticket was raised. Only filled in the
    /// response to creating a ticket; reading one later returns null.
    /// </summary>
    public TriageDto? Triage { get; set; }
}
