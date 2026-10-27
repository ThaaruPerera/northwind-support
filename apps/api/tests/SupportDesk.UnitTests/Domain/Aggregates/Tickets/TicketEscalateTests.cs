using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Exceptions;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Domain.Aggregates.Tickets;

public class TicketEscalateTests
{
    private static readonly DateTime Raised = FixedClock.DefaultNow;
    private static readonly DateTime Later = Raised.AddHours(6);
    private static readonly DateTime NewDue = Later.AddHours(4);

    /// <summary>A medium ticket owned by agent 4, due at the end of its 24 hour window.</summary>
    private static Ticket OpenTicket(TicketPriority priority = TicketPriority.Medium)
    {
        var ticket = new TicketBuilder().WithPriority(priority).WithStatus(TicketStatus.Open).Build();
        ticket.ApplyTriage(Raised.AddHours(24), assignedAgentId: 4, unassignedReason: null, Raised);

        return ticket;
    }

    [Fact]
    public void The_ticket_takes_the_new_priority_and_due_date_and_restarts_its_clock()
    {
        var ticket = OpenTicket();

        ticket.Escalate(TicketPriority.High, NewDue, toAgentId: 4, null, "Customer is waiting", "Dana", Later);

        Assert.Equal(TicketPriority.High, ticket.Priority);
        Assert.Equal(NewDue, ticket.DueAtUtc);
        Assert.Equal(Later, ticket.SlaStartedAtUtc);
        Assert.Equal(Later, ticket.UpdatedAtUtc);
    }

    [Fact]
    public void The_history_row_records_the_before_and_the_after()
    {
        var ticket = OpenTicket();

        var escalation = ticket.Escalate(TicketPriority.High, NewDue, toAgentId: 7, null, "Customer is waiting", "Dana", Later);

        Assert.Equal(TicketPriority.Medium, escalation.FromPriority);
        Assert.Equal(TicketPriority.High, escalation.ToPriority);
        Assert.Equal(4, escalation.FromAgentId);
        Assert.Equal(7, escalation.ToAgentId);
        Assert.Equal(Raised.AddHours(24), escalation.FromDueAtUtc);
        Assert.Equal(NewDue, escalation.ToDueAtUtc);
        Assert.Equal("Customer is waiting", escalation.Reason);
        Assert.Equal("Dana", escalation.EscalatedBy);
        Assert.Equal(Later, escalation.EscalatedAtUtc);
    }

    [Fact]
    public void Keeping_the_same_agent_is_recorded_as_no_change_of_agent()
    {
        var ticket = OpenTicket();

        var escalation = ticket.Escalate(TicketPriority.High, NewDue, toAgentId: 4, null, "Customer is waiting", "Dana", Later);

        Assert.Equal(4, ticket.AssignedAgentId);
        Assert.Equal(escalation.FromAgentId, escalation.ToAgentId);
    }

    [Fact]
    public void Handing_the_ticket_to_another_agent_changes_the_owner()
    {
        var ticket = OpenTicket();

        ticket.Escalate(TicketPriority.High, NewDue, toAgentId: 9, null, "Customer is waiting", "Dana", Later);

        Assert.Equal(9, ticket.AssignedAgentId);
    }

    [Fact]
    public void When_nobody_can_take_it_the_ticket_is_unassigned_with_the_reason()
    {
        var ticket = OpenTicket();

        var escalation = ticket.Escalate(
            TicketPriority.High, NewDue, toAgentId: null, "Priya is no longer eligible.", "Customer is waiting", "Dana", Later);

        Assert.Null(ticket.AssignedAgentId);
        Assert.Equal("Priya is no longer eligible.", ticket.UnassignedReason);
        Assert.Null(escalation.ToAgentId);
        Assert.Equal(4, escalation.FromAgentId);
    }

    [Fact]
    public void Getting_an_agent_again_clears_an_old_unassigned_reason()
    {
        var ticket = new TicketBuilder().WithStatus(TicketStatus.Open).Build();
        ticket.ApplyTriage(Raised.AddHours(24), assignedAgentId: null, "Nobody is eligible.", Raised);

        var escalation = ticket.Escalate(TicketPriority.High, NewDue, toAgentId: 5, null, "Customer is waiting", "Dana", Later);

        Assert.Equal(5, ticket.AssignedAgentId);
        Assert.Null(ticket.UnassignedReason);
        Assert.Null(escalation.FromAgentId);
    }

    [Fact]
    public void A_ticket_that_had_no_due_date_is_recorded_with_none_before()
    {
        var ticket = new TicketBuilder().WithStatus(TicketStatus.Open).Build();

        var escalation = ticket.Escalate(TicketPriority.High, NewDue, toAgentId: null, "none", "Customer is waiting", "Dana", Later);

        Assert.Null(escalation.FromDueAtUtc);
    }

    [Fact]
    public void The_reason_and_the_person_are_stored_without_surrounding_whitespace()
    {
        var escalation = OpenTicket().Escalate(
            TicketPriority.High, NewDue, 4, null, "  Customer is waiting  ", "  Dana  ", Later);

        Assert.Equal("Customer is waiting", escalation.Reason);
        Assert.Equal("Dana", escalation.EscalatedBy);
    }

    [Fact]
    public void A_critical_ticket_cannot_be_escalated_and_the_message_says_why()
    {
        var ticket = OpenTicket(TicketPriority.Critical);

        var error = Assert.Throws<BusinessRuleViolationException>(
            () => ticket.Escalate(TicketPriority.Critical, NewDue, 4, null, "Customer is waiting", "Dana", Later));

        Assert.Equal("Ticket TCK-0001 is already Critical and cannot be escalated.", error.Message);
    }

    [Theory]
    [InlineData(TicketStatus.Resolved)]
    [InlineData(TicketStatus.Closed)]
    public void A_finished_ticket_cannot_be_escalated_and_the_message_says_which(TicketStatus status)
    {
        var ticket = new TicketBuilder().WithStatus(status).Build();

        var error = Assert.Throws<BusinessRuleViolationException>(
            () => ticket.Escalate(TicketPriority.High, NewDue, 4, null, "Customer is waiting", "Dana", Later));

        Assert.Equal($"Ticket TCK-0001 is {status} and cannot be escalated.", error.Message);
    }

    [Fact]
    public void A_refused_escalation_leaves_the_ticket_exactly_as_it_was()
    {
        var ticket = OpenTicket(TicketPriority.Critical);

        Assert.Throws<BusinessRuleViolationException>(
            () => ticket.Escalate(TicketPriority.Critical, NewDue, 9, null, "Customer is waiting", "Dana", Later));

        Assert.Equal(TicketPriority.Critical, ticket.Priority);
        Assert.Equal(4, ticket.AssignedAgentId);
        Assert.Equal(Raised, ticket.SlaStartedAtUtc);
    }
}
