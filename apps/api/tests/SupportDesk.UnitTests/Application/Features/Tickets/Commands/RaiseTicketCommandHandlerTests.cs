using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Application.Features.Tickets.Commands;

public class RaiseTicketCommandHandlerTests
{
    private static CreateTicketRequest Request(TicketPriority? priority = null) =>
        new("Cannot log in to the portal", "Happens for every user since this morning.", 1, 3, priority);

    [Fact]
    public async Task HandleAsync_StampsReferenceStatusAndTimestamps()
    {
        var context = new TicketCommandTestContext();

        await context.RaiseTicket.HandleAsync(Request(TicketPriority.High), CancellationToken.None);

        var created = context.AddedTicket;
        Assert.NotNull(created);
        Assert.Equal("TCK-0041", created.Reference);
        Assert.Equal(TicketStatus.New, created.Status);
        Assert.Equal(TicketPriority.High, created.Priority);
        Assert.Equal(FixedClock.DefaultNow, created.CreatedAtUtc);
        Assert.Equal(FixedClock.DefaultNow, created.UpdatedAtUtc);
        context.VerifySaved(Times.Once());
    }

    [Fact]
    public async Task HandleAsync_WithoutRequestedPriority_FallsBackToMedium()
    {
        var context = new TicketCommandTestContext();

        await context.RaiseTicket.HandleAsync(Request(), CancellationToken.None);

        Assert.NotNull(context.AddedTicket);
        Assert.Equal(TicketPriority.Medium, context.AddedTicket.Priority);
    }

    [Fact]
    public async Task HandleAsync_WhenCustomerDoesNotExist_Throws()
    {
        var context = new TicketCommandTestContext();
        context.Customers
            .Setup(c => c.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Customer?)null);

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => context.RaiseTicket.HandleAsync(Request(), CancellationToken.None));

        Assert.Contains("Customer", exception.Message);
        context.VerifySaved(Times.Never());
    }

    [Fact]
    public async Task HandleAsync_WhenCategoryDoesNotExist_Throws()
    {
        var context = new TicketCommandTestContext();
        context.Categories
            .Setup(c => c.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Category?)null);

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => context.RaiseTicket.HandleAsync(Request(), CancellationToken.None));

        Assert.Contains("Category", exception.Message);
        context.VerifySaved(Times.Never());
    }

    [Fact]
    public async Task HandleAsync_DueDateComesFromThePriorityAndStartsTheSlaClock()
    {
        var context = new TicketCommandTestContext();

        await context.RaiseTicket.HandleAsync(Request(TicketPriority.High), CancellationToken.None);

        Assert.Equal(FixedClock.DefaultNow.AddHours(8), context.AddedTicket!.DueAtUtc);
        Assert.Equal(FixedClock.DefaultNow, context.AddedTicket.SlaStartedAtUtc);
    }

    [Fact]
    public async Task HandleAsync_APremiumCustomerGetsHalfTheWindow()
    {
        var context = new TicketCommandTestContext();
        context.Customers
            .Setup(c => c.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TicketCommandTestContext.PremiumCustomer);

        await context.RaiseTicket.HandleAsync(Request(TicketPriority.High), CancellationToken.None);

        Assert.Equal(FixedClock.DefaultNow.AddHours(4), context.AddedTicket!.DueAtUtc);
    }

    [Fact]
    public async Task HandleAsync_ACategoryThatForcesCriticalOverridesTheRequestedPriority()
    {
        var context = new TicketCommandTestContext();
        context.Categories
            .Setup(c => c.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TicketCommandTestContext.SecurityCategory);

        await context.RaiseTicket.HandleAsync(Request(TicketPriority.Low), CancellationToken.None);

        Assert.Equal(TicketPriority.Critical, context.AddedTicket!.Priority);
        Assert.Equal(FixedClock.DefaultNow.AddHours(4), context.AddedTicket.DueAtUtc);
    }

    [Fact]
    public async Task HandleAsync_AssignsTheAgentWithTheFewestOpenTickets()
    {
        var context = new TicketCommandTestContext();
        context.TeamIs(
            new AgentCandidate(1, "Busy", true, 10, 6, []),
            new AgentCandidate(2, "Free", true, 10, 1, []));

        await context.RaiseTicket.HandleAsync(Request(), CancellationToken.None);

        Assert.Equal(2, context.AddedTicket!.AssignedAgentId);
        Assert.Null(context.AddedTicket.UnassignedReason);
    }

    [Fact]
    public async Task HandleAsync_WithNobodyEligible_StillCreatesTheTicketUnassignedWithTheReason()
    {
        var context = new TicketCommandTestContext();
        context.TeamIs(new AgentCandidate(1, "Full", true, 6, 6, []));

        var created = await context.RaiseTicket.HandleAsync(Request(), CancellationToken.None);

        Assert.Null(context.AddedTicket!.AssignedAgentId);
        Assert.Equal("Every active agent is at their open ticket limit.", context.AddedTicket.UnassignedReason);
        Assert.Equal("Every active agent is at their open ticket limit.", created.Triage!.AssignmentReason);
        context.VerifySaved(Times.Once());
    }

    [Fact]
    public async Task HandleAsync_WithNoAgentsAtAll_IsNotAnError()
    {
        var context = new TicketCommandTestContext();

        var created = await context.RaiseTicket.HandleAsync(Request(), CancellationToken.None);

        Assert.Equal("There are no active agents.", created.Triage!.AssignmentReason);
    }

    [Fact]
    public async Task HandleAsync_ReturnsAReasonForEachOfTheThreeDecisions()
    {
        var context = new TicketCommandTestContext();
        context.TeamIs(new AgentCandidate(1, "Alex Turner", true, 10, 5, []));

        var created = await context.RaiseTicket.HandleAsync(Request(TicketPriority.High), CancellationToken.None);

        Assert.Equal("High, as requested.", created.Triage!.PriorityReason);
        Assert.Equal("High priority has a base window of 8h.", created.Triage.SlaReason);
        Assert.Equal("Alex Turner has the fewest open tickets (5) of 1 eligible agent.", created.Triage.AssignmentReason);
    }
}
