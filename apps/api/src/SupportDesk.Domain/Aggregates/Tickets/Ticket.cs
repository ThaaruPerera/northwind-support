using System.Linq.Expressions;
using SupportDesk.Domain.Common;
using SupportDesk.Domain.Exceptions;

namespace SupportDesk.Domain.Aggregates.Tickets;

/// <summary>
/// A support request raised by a customer. Every change to a ticket goes through one of its
/// methods, so its rules cannot be bypassed.
/// </summary>
/// <remarks>
/// The customer, the category and the assigned agent are other aggregates and are referenced
/// by id only.
/// </remarks>
public sealed class Ticket : AggregateRoot
{
    private Ticket()
    {
        // For EF Core.
    }

    /// <summary>Human-friendly identifier shown to customers, e.g. TCK-0042.</summary>
    public string Reference { get; private set; } = string.Empty;

    public string Title { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public int CustomerId { get; private set; }

    public int CategoryId { get; private set; }

    /// <summary>Null while the ticket is waiting for an owner.</summary>
    public int? AssignedAgentId { get; private set; }

    public TicketPriority Priority { get; private set; }

    public TicketStatus Status { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    /// <summary>When the team has committed to responding by. Null when no SLA applies.</summary>
    public DateTime? DueAtUtc { get; private set; }

    public DateTime? ResolvedAtUtc { get; private set; }

    /// <summary>
    /// When the current SLA window began: the ticket's creation, or the latest escalation. At risk
    /// is measured from here, so an escalated ticket gets a fresh window and not a short one.
    /// </summary>
    public DateTime SlaStartedAtUtc { get; private set; }

    /// <summary>Why the ticket has no owner. Null while it has one, or if it was never explained.</summary>
    public string? UnassignedReason { get; private set; }

    /// <summary>True while the ticket still needs work from an agent.</summary>
    public bool IsOpen => Status is not (TicketStatus.Resolved or TicketStatus.Closed);

    /// <summary>
    /// <see cref="IsOpen"/> as an expression, so a query can ask the database the same question.
    /// Counting an agent's open tickets uses this, so "open" is defined once.
    /// </summary>
    public static Expression<Func<Ticket, bool>> IsOpenExpression { get; } =
        ticket => ticket.Status != TicketStatus.Resolved && ticket.Status != TicketStatus.Closed;

    /// <summary>
    /// Raises a new ticket for a customer. It starts as <see cref="TicketStatus.New"/>, with no
    /// owner and no due date until <see cref="ApplyTriage"/> says otherwise.
    /// </summary>
    public static Ticket Raise(
        string reference,
        string title,
        string description,
        int customerId,
        int categoryId,
        TicketPriority priority,
        DateTime nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        return new Ticket
        {
            Reference = reference,
            Title = title.Trim(),
            Description = description.Trim(),
            CustomerId = customerId,
            CategoryId = categoryId,
            Priority = priority,
            Status = TicketStatus.New,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
            SlaStartedAtUtc = nowUtc
        };
    }

    /// <summary>
    /// Records the outcome of triage on a ticket that has just been raised: when it is due and
    /// who owns it, or why nobody does. The SLA clock starts when the ticket was raised.
    /// </summary>
    public void ApplyTriage(DateTime dueAtUtc, int? assignedAgentId, string? unassignedReason, DateTime nowUtc)
    {
        DueAtUtc = dueAtUtc;
        AssignTo(assignedAgentId, nowUtc);

        UnassignedReason = assignedAgentId is null ? unassignedReason : null;
    }

    /// <summary>
    /// Moves the ticket through its lifecycle, keeping the resolution timestamp in step.
    /// </summary>
    /// <exception cref="BusinessRuleViolationException">The ticket is closed.</exception>
    public void ChangeStatus(TicketStatus status, DateTime nowUtc)
    {
        if (Status == TicketStatus.Closed && status != TicketStatus.Closed)
        {
            throw new BusinessRuleViolationException(
                $"Ticket {Reference} is closed and cannot be moved to {status}.");
        }

        ResolvedAtUtc = status switch
        {
            TicketStatus.Resolved => nowUtc,
            TicketStatus.Closed => ResolvedAtUtc ?? nowUtc,
            _ => null
        };

        Status = status;
        UpdatedAtUtc = nowUtc;
    }

    /// <summary>
    /// Hands the ticket to an agent, or takes it back off them when <paramref name="agentId"/>
    /// is null. Whether the agent may take it is checked by the caller, which can see agents.
    /// </summary>
    /// <remarks>
    /// An explanation of why a ticket was unassigned goes stale as soon as someone decides who has
    /// it, so any assignment through here clears it.
    /// </remarks>
    public void AssignTo(int? agentId, DateTime nowUtc)
    {
        AssignedAgentId = agentId;
        UnassignedReason = null;
        UpdatedAtUtc = nowUtc;
    }
}
