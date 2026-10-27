using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.UnitTests.Domain.Aggregates.Tickets;

public class PriorityPolicyTests
{
    private static readonly Category Ordinary = new("General", requiresSpecialist: false, forcesCriticalPriority: false);
    private static readonly Category Security = new("Security", requiresSpecialist: true, forcesCriticalPriority: true);

    [Theory]
    [InlineData(TicketPriority.Low)]
    [InlineData(TicketPriority.Medium)]
    [InlineData(TicketPriority.High)]
    [InlineData(TicketPriority.Critical)]
    public void The_requested_priority_is_used(TicketPriority requested)
    {
        var decision = PriorityPolicy.Decide(requested, Ordinary);

        Assert.Equal(requested, decision.Priority);
        Assert.Equal($"{requested}, as requested.", decision.Reason);
    }

    [Fact]
    public void No_requested_priority_defaults_to_medium()
    {
        var decision = PriorityPolicy.Decide(null, Ordinary);

        Assert.Equal(TicketPriority.Medium, decision.Priority);
        Assert.Equal("Medium, because no priority was requested.", decision.Reason);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(TicketPriority.Low)]
    [InlineData(TicketPriority.High)]
    public void A_category_that_forces_critical_overrides_whatever_was_requested(TicketPriority? requested)
    {
        var decision = PriorityPolicy.Decide(requested, Security);

        Assert.Equal(TicketPriority.Critical, decision.Priority);
        Assert.Equal("Critical, because Security tickets are always Critical.", decision.Reason);
    }

    [Fact]
    public void The_rule_follows_the_flag_not_the_name()
    {
        var renamed = new Category("Incident response", requiresSpecialist: false, forcesCriticalPriority: true);
        var lookalike = new Category("Security", requiresSpecialist: false, forcesCriticalPriority: false);

        Assert.Equal(TicketPriority.Critical, PriorityPolicy.Decide(TicketPriority.Low, renamed).Priority);
        Assert.Equal(TicketPriority.Low, PriorityPolicy.Decide(TicketPriority.Low, lookalike).Priority);
    }

    [Theory]
    [InlineData(TicketPriority.Low, TicketPriority.Medium)]
    [InlineData(TicketPriority.Medium, TicketPriority.High)]
    [InlineData(TicketPriority.High, TicketPriority.Critical)]
    public void Raising_goes_up_exactly_one_level(TicketPriority from, TicketPriority to) =>
        Assert.Equal(to, PriorityPolicy.Raise(from));

    [Fact]
    public void Critical_cannot_be_raised() =>
        Assert.Null(PriorityPolicy.Raise(TicketPriority.Critical));
}
