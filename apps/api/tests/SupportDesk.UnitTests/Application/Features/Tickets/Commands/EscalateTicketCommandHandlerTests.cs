using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Exceptions;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Application.Features.Tickets.Commands;

public class EscalateTicketCommandHandlerTests
{
    private static readonly DateTime Raised = FixedClock.DefaultNow.AddHours(-6);

    private static readonly EscalateTicketRequest Request = new("Customer is waiting");

    /// <summary>A medium ticket owned by agent 4, raised six hours before "now", due after 24 hours.</summary>
    private static Ticket OpenTicket(TicketPriority priority = TicketPriority.Medium, TicketStatus status = TicketStatus.Open)
    {
        var ticket = new TicketBuilder().WithPriority(priority).WithStatus(status).AssignedTo(4).Build();
        ticket.ApplyTriage(Raised.AddHours(24), assignedAgentId: 4, unassignedReason: null, Raised);

        return ticket;
    }

    private static AgentCandidate Agent(int id, string name, bool active = true, int open = 2, int limit = 10) =>
        new(id, name, active, limit, open, []);

    [Fact]
    public async Task HandleAsync_ForAnUnknownTicket_Throws()
    {
        var context = new TicketCommandTestContext();

        await Assert.ThrowsAsync<NotFoundException>(
            () => context.EscalateTicket.HandleAsync(999, Request, CancellationToken.None));

        context.VerifySaved(Times.Never());
    }

    [Fact]
    public async Task HandleAsync_ForACriticalTicket_IsRefusedAndNothingIsSaved()
    {
        var context = new TicketCommandTestContext();
        context.ExistingTicket(OpenTicket(TicketPriority.Critical));

        var error = await Assert.ThrowsAsync<BusinessRuleViolationException>(
            () => context.EscalateTicket.HandleAsync(0, Request, CancellationToken.None));

        Assert.Contains("Critical", error.Message);
        Assert.Null(context.AddedEscalation);
        context.VerifySaved(Times.Never());
    }

    [Theory]
    [InlineData(TicketStatus.Resolved)]
    [InlineData(TicketStatus.Closed)]
    public async Task HandleAsync_ForAFinishedTicket_IsRefusedAndNothingIsSaved(TicketStatus status)
    {
        var context = new TicketCommandTestContext();
        context.ExistingTicket(OpenTicket(status: status));

        var error = await Assert.ThrowsAsync<BusinessRuleViolationException>(
            () => context.EscalateTicket.HandleAsync(0, Request, CancellationToken.None));

        Assert.Contains(status.ToString(), error.Message);
        Assert.Null(context.AddedEscalation);
        context.VerifySaved(Times.Never());
    }

    [Fact]
    public async Task HandleAsync_RaisesThePriorityOneLevelAndCountsTheDueDateFromNow()
    {
        var context = new TicketCommandTestContext();
        var ticket = context.ExistingTicket(OpenTicket());

        await context.EscalateTicket.HandleAsync(0, Request, CancellationToken.None);

        Assert.Equal(TicketPriority.High, ticket.Priority);
        Assert.Equal(FixedClock.DefaultNow.AddHours(8), ticket.DueAtUtc);
        Assert.Equal(FixedClock.DefaultNow, ticket.SlaStartedAtUtc);
    }

    [Fact]
    public async Task HandleAsync_APremiumCustomerGetsHalfTheNewWindow()
    {
        var context = new TicketCommandTestContext();
        context.Customers
            .Setup(c => c.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TicketCommandTestContext.PremiumCustomer);
        var ticket = context.ExistingTicket(OpenTicket());

        await context.EscalateTicket.HandleAsync(0, Request, CancellationToken.None);

        Assert.Equal(FixedClock.DefaultNow.AddHours(4), ticket.DueAtUtc);
    }

    [Fact]
    public async Task HandleAsync_SavesTheTicketAndItsHistoryRowTogetherInOneSave()
    {
        var context = new TicketCommandTestContext();
        context.ExistingTicket(OpenTicket());

        await context.EscalateTicket.HandleAsync(0, Request, CancellationToken.None);

        var recorded = context.AddedEscalation;
        Assert.NotNull(recorded);
        Assert.Equal(TicketPriority.Medium, recorded.FromPriority);
        Assert.Equal(TicketPriority.High, recorded.ToPriority);
        Assert.Equal("Customer is waiting", recorded.Reason);
        Assert.Equal("Dana", recorded.EscalatedBy);
        Assert.Equal(FixedClock.DefaultNow, recorded.EscalatedAtUtc);
        context.VerifySaved(Times.Once());
    }

    [Fact]
    public async Task HandleAsync_ReturnsTheUpdatedTicketAndTheRecordedEscalation()
    {
        var context = new TicketCommandTestContext();
        context.ExistingTicket(OpenTicket());

        var result = await context.EscalateTicket.HandleAsync(0, Request, CancellationToken.None);

        Assert.Equal("TCK-0041", result.Ticket.Reference);
        Assert.Equal("Customer is waiting", result.Escalation.Reason);
    }

    [Fact]
    public async Task HandleAsync_TheCurrentAgentKeepsTheTicketWhileStillEligible()
    {
        var context = new TicketCommandTestContext();
        var ticket = context.ExistingTicket(OpenTicket());
        context.TeamIs(Agent(4, "Current", open: 3), Agent(9, "Quieter", open: 0));

        await context.EscalateTicket.HandleAsync(0, Request, CancellationToken.None);

        Assert.Equal(4, ticket.AssignedAgentId);
        Assert.Equal(4, context.AddedEscalation!.ToAgentId);
    }

    [Fact]
    public async Task HandleAsync_AnAgentAtTheirLimitStillKeepsATicketTheyAlreadyHold()
    {
        var context = new TicketCommandTestContext();
        var ticket = context.ExistingTicket(OpenTicket());
        context.TeamIs(Agent(4, "Full", open: 6, limit: 6), Agent(9, "Quieter", open: 0));

        await context.EscalateTicket.HandleAsync(0, Request, CancellationToken.None);

        Assert.Equal(4, ticket.AssignedAgentId);
    }

    [Fact]
    public async Task HandleAsync_AnAgentWhoIsNoLongerEligibleIsReplaced()
    {
        var context = new TicketCommandTestContext();
        var ticket = context.ExistingTicket(OpenTicket());
        context.TeamIs(Agent(4, "Away", active: false), Agent(9, "Replacement"));

        await context.EscalateTicket.HandleAsync(0, Request, CancellationToken.None);

        Assert.Equal(9, ticket.AssignedAgentId);
        Assert.Equal(4, context.AddedEscalation!.FromAgentId);
        Assert.Equal(9, context.AddedEscalation.ToAgentId);
    }

    [Fact]
    public async Task HandleAsync_WhenNobodyCanTakeIt_TheTicketIsStillEscalatedAndLeftUnassignedWithAReason()
    {
        var context = new TicketCommandTestContext();
        var ticket = context.ExistingTicket(OpenTicket());
        context.TeamIs(Agent(4, "Away", active: false));

        await context.EscalateTicket.HandleAsync(0, Request, CancellationToken.None);

        Assert.Equal(TicketPriority.High, ticket.Priority);
        Assert.Null(ticket.AssignedAgentId);
        Assert.Equal("Away is no longer eligible. There are no active agents.", ticket.UnassignedReason);
        Assert.Null(context.AddedEscalation!.ToAgentId);
        context.VerifySaved(Times.Once());
    }

    [Fact]
    public async Task HandleAsync_RecordsTheSignedInAgentAsWhoEscalated()
    {
        var context = new TicketCommandTestContext();
        context.SignedInAs(TicketCommandTestContext.SignedInAgentId, new Agent("Priya Nair", "priya@support.test", 8, FixedClock.DefaultNow));
        context.ExistingTicket(OpenTicket());

        await context.EscalateTicket.HandleAsync(0, Request, CancellationToken.None);

        Assert.Equal("Priya Nair", context.AddedEscalation!.EscalatedBy);
    }

    [Fact]
    public async Task HandleAsync_WithNobodySignedIn_IsRefusedAndNothingIsSaved()
    {
        var context = new TicketCommandTestContext();
        context.SignedInAs(null, null);
        var ticket = context.ExistingTicket(OpenTicket());

        await Assert.ThrowsAsync<AuthenticationFailedException>(
            () => context.EscalateTicket.HandleAsync(0, Request, CancellationToken.None));

        Assert.Equal(TicketPriority.Medium, ticket.Priority);
        Assert.Null(context.AddedEscalation);
        context.VerifySaved(Times.Never());
    }

    [Fact]
    public async Task HandleAsync_WithADeactivatedAgentHoldingAnOldToken_IsRefused()
    {
        var context = new TicketCommandTestContext();
        var gone = new Agent("Dana", "dana@support.test", 10, FixedClock.DefaultNow);
        gone.Deactivate();
        context.SignedInAs(TicketCommandTestContext.SignedInAgentId, gone);
        context.ExistingTicket(OpenTicket());

        await Assert.ThrowsAsync<AuthenticationFailedException>(
            () => context.EscalateTicket.HandleAsync(0, Request, CancellationToken.None));

        context.VerifySaved(Times.Never());
    }

    [Fact]
    public async Task HandleAsync_WithATokenForAnAgentWhoNoLongerExists_IsRefused()
    {
        var context = new TicketCommandTestContext();
        context.SignedInAs(TicketCommandTestContext.SignedInAgentId, null);
        context.ExistingTicket(OpenTicket());

        await Assert.ThrowsAsync<AuthenticationFailedException>(
            () => context.EscalateTicket.HandleAsync(0, Request, CancellationToken.None));
    }

    [Fact]
    public async Task HandleAsync_ChecksWhoIsAskingBeforeLookingForTheTicket()
    {
        var context = new TicketCommandTestContext();
        context.SignedInAs(null, null);

        // The ticket does not exist either, but a stranger is told only to sign in.
        await Assert.ThrowsAsync<AuthenticationFailedException>(
            () => context.EscalateTicket.HandleAsync(999, Request, CancellationToken.None));
    }

    [Fact]
    public async Task HandleAsync_ShortensANameLongerThanTheHistoryColumn()
    {
        var context = new TicketCommandTestContext();
        context.SignedInAs(
            TicketCommandTestContext.SignedInAgentId,
            new Agent(new string('N', 150), "long@support.test", 10, FixedClock.DefaultNow));
        context.ExistingTicket(OpenTicket());

        await context.EscalateTicket.HandleAsync(0, Request, CancellationToken.None);

        Assert.Equal(100, context.AddedEscalation!.EscalatedBy.Length);
        context.VerifySaved(Times.Once());
    }
}
