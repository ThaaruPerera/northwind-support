using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Services;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Domain.Services;

public class TriageServiceTests
{
    private static readonly DateTime Now = FixedClock.DefaultNow;

    private readonly TriageService _triage = new(new SlaPolicy(SlaTestSettings.Default()));

    private static readonly CategoryRequirement General = new(1, "General", false, false);
    private static readonly CategoryRequirement Security = new(5, "Security", true, true);

    private static readonly AgentCandidate Generalist = new(1, "Alex Turner", true, 10, 5, [1]);
    private static readonly AgentCandidate SecuritySpecialist = new(3, "Priya Nair", true, 8, 4, [4, 5]);

    [Fact]
    public void The_priority_decides_the_window_which_decides_the_due_date()
    {
        var decision = _triage.Triage(TicketPriority.High, General, CustomerTier.Standard, [Generalist], Now);

        Assert.Equal(TicketPriority.High, decision.Priority.Priority);
        Assert.Equal(TimeSpan.FromHours(8), decision.Window.Duration);
        Assert.Equal(Now.AddHours(8), decision.DueAtUtc);
    }

    [Fact]
    public void A_forced_critical_priority_shortens_the_window_even_though_a_low_one_was_asked_for()
    {
        var decision = _triage.Triage(TicketPriority.Low, Security, CustomerTier.Standard, [SecuritySpecialist], Now);

        Assert.Equal(TicketPriority.Critical, decision.Priority.Priority);
        Assert.Equal(Now.AddHours(4), decision.DueAtUtc);
    }

    [Fact]
    public void A_premium_customer_on_a_forced_critical_category_gets_the_shortest_window()
    {
        var decision = _triage.Triage(null, Security, CustomerTier.Premium, [SecuritySpecialist], Now);

        Assert.Equal(Now.AddHours(2), decision.DueAtUtc);
    }

    [Fact]
    public void The_owner_is_chosen_on_the_category()
    {
        var decision = _triage.Triage(null, Security, CustomerTier.Standard, [Generalist, SecuritySpecialist], Now);

        Assert.Equal(SecuritySpecialist.Id, decision.Assignment.Agent?.Id);
    }

    [Fact]
    public void Nobody_eligible_still_gives_a_priority_and_a_due_date_and_a_reason()
    {
        var decision = _triage.Triage(null, Security, CustomerTier.Standard, [Generalist], Now);

        Assert.Null(decision.Assignment.Agent);
        Assert.Equal("No active agent specialises in Security.", decision.Assignment.Reason);
        Assert.Equal(TicketPriority.Critical, decision.Priority.Priority);
        Assert.Equal(Now.AddHours(4), decision.DueAtUtc);
    }
}
