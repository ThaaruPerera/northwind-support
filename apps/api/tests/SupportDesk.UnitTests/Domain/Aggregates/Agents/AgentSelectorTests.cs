using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;

namespace SupportDesk.UnitTests.Domain.Aggregates.Agents;

public class AgentSelectorTests
{
    private const int General = 1;
    private const int Billing = 2;
    private const int Technical = 3;
    private const int Outage = 4;
    private const int Security = 5;

    private static readonly CategoryRequirement GeneralCategory = new(General, "General", false);
    private static readonly CategoryRequirement BillingCategory = new(Billing, "Billing", true);
    private static readonly CategoryRequirement TechnicalCategory = new(Technical, "Technical", false);
    private static readonly CategoryRequirement OutageCategory = new(Outage, "Outage", true);
    private static readonly CategoryRequirement SecurityCategory = new(Security, "Security", true);

    // The seed table from the assignment: id, name, active, open, limit, specialisations.
    private static readonly AgentCandidate Alex = Agent(1, "Alex Turner", true, 5, 10, General, Technical);
    private static readonly AgentCandidate Ben = Agent(2, "Ben Osei", true, 3, 8, Technical, Outage);
    private static readonly AgentCandidate Priya = Agent(3, "Priya Nair", true, 4, 8, Outage, Security);
    private static readonly AgentCandidate Sara = Agent(4, "Sara Lindqvist", true, 2, 6, General, Billing);
    private static readonly AgentCandidate Marcus = Agent(5, "Marcus Doyle", true, 6, 6, Billing, Security);
    private static readonly AgentCandidate Yuki = Agent(6, "Yuki Tanaka", false, 0, 8, Technical);

    private static readonly AgentCandidate[] Team = [Alex, Ben, Priya, Sara, Marcus, Yuki];

    [Theory]
    [InlineData(Billing, "Sara Lindqvist")]   // Marcus is the other specialist, and he is full.
    [InlineData(Security, "Priya Nair")]      // Marcus is full again.
    [InlineData(Outage, "Ben Osei")]          // 3 open beats Priya's 4.
    [InlineData(Technical, "Sara Lindqvist")] // Not a specialist category, so Sara's 2 open beats Ben's 3. Yuki has 0 but is inactive.
    [InlineData(General, "Sara Lindqvist")]   // 2 open is the fewest of everyone with room.
    public void The_seed_team_gets_the_expected_agent(int categoryId, string expected)
    {
        var decision = AgentSelector.Select(Team, CategoryFor(categoryId));

        Assert.Equal(expected, decision.Agent?.FullName);
    }

    [Fact]
    public void The_agent_with_the_fewest_open_tickets_wins()
    {
        var decision = AgentSelector.Select([Agent(1, "A", true, 4, 10), Agent(2, "B", true, 1, 10), Agent(3, "C", true, 7, 10)], GeneralCategory);

        Assert.Equal(2, decision.Agent?.Id);
    }

    [Fact]
    public void An_inactive_agent_is_never_chosen_however_few_tickets_they_have()
    {
        var decision = AgentSelector.Select([Agent(1, "Idle", false, 0, 10), Agent(2, "Busy", true, 9, 10)], GeneralCategory);

        Assert.Equal(2, decision.Agent?.Id);
    }

    [Fact]
    public void An_agent_one_below_their_limit_is_eligible()
    {
        var decision = AgentSelector.Select([Agent(1, "Nearly full", true, 5, 6)], GeneralCategory);

        Assert.Equal(1, decision.Agent?.Id);
    }

    [Fact]
    public void An_agent_exactly_at_their_limit_is_not_eligible()
    {
        var decision = AgentSelector.Select([Agent(1, "Full", true, 6, 6)], GeneralCategory);

        Assert.Null(decision.Agent);
    }

    [Fact]
    public void A_specialist_category_ignores_agents_without_the_specialisation_even_with_room()
    {
        var decision = AgentSelector.Select([Agent(1, "Generalist", true, 0, 10, General), Agent(2, "Specialist", true, 8, 10, Billing)], BillingCategory);

        Assert.Equal(2, decision.Agent?.Id);
    }

    [Fact]
    public void A_category_that_does_not_need_a_specialist_accepts_any_active_agent_with_room()
    {
        var decision = AgentSelector.Select([Agent(1, "Billing only", true, 0, 10, Billing)], GeneralCategory);

        Assert.Equal(1, decision.Agent?.Id);
    }

    [Fact]
    public void A_tie_goes_to_the_lowest_agent_id_whatever_order_the_agents_arrive_in()
    {
        var a = Agent(7, "Seven", true, 2, 10);
        var b = Agent(3, "Three", true, 2, 10);
        var c = Agent(9, "Nine", true, 2, 10);

        Assert.Equal(3, AgentSelector.Select([a, b, c], GeneralCategory).Agent?.Id);
        Assert.Equal(3, AgentSelector.Select([c, b, a], GeneralCategory).Agent?.Id);
        Assert.Equal(3, AgentSelector.Select([b, c, a], GeneralCategory).Agent?.Id);
    }

    [Fact]
    public void The_reason_for_a_choice_names_the_agent_and_how_many_competed()
    {
        var decision = AgentSelector.Select(Team, OutageCategory);

        Assert.Equal("Ben Osei has the fewest open tickets (3) of 2 eligible Outage specialists.", decision.Reason);
    }

    [Fact]
    public void Nobody_eligible_is_a_normal_outcome_with_no_agent_and_a_reason()
    {
        var decision = AgentSelector.Select([], GeneralCategory);

        Assert.Null(decision.Agent);
        Assert.Equal("There are no active agents.", decision.Reason);
    }

    [Fact]
    public void No_specialist_at_all_says_so()
    {
        var decision = AgentSelector.Select([Agent(1, "Generalist", true, 0, 10, General)], SecurityCategory);

        Assert.Null(decision.Agent);
        Assert.Equal("No active agent specialises in Security.", decision.Reason);
    }

    [Fact]
    public void An_inactive_specialist_does_not_count_as_a_specialist()
    {
        var decision = AgentSelector.Select([Agent(1, "Away", false, 0, 10, Billing)], BillingCategory);

        Assert.Equal("No active agent specialises in Billing.", decision.Reason);
    }

    [Fact]
    public void Specialists_who_are_all_full_say_so()
    {
        var decision = AgentSelector.Select([Marcus], BillingCategory);

        Assert.Null(decision.Agent);
        Assert.Equal("Every active Billing specialist is at their open ticket limit.", decision.Reason);
    }

    [Fact]
    public void Agents_who_are_all_full_say_so_for_a_general_category()
    {
        var decision = AgentSelector.Select([Marcus], GeneralCategory);

        Assert.Equal("Every active agent is at their open ticket limit.", decision.Reason);
    }

    public class OnEscalation
    {
        [Fact]
        public void The_current_agent_keeps_the_ticket_while_still_eligible()
        {
            // Sara is not the fewest in the list, but she already holds the ticket.
            var decision = AgentSelector.SelectForEscalation(Alex.Id, Team, GeneralCategory);

            Assert.Equal(Alex.Id, decision.Agent?.Id);
            Assert.Equal("Alex Turner keeps the ticket; still eligible.", decision.Reason);
        }

        [Fact]
        public void An_agent_at_their_limit_keeps_a_ticket_they_already_hold()
        {
            // 6 of 6 includes this very ticket, so they are not taking on anything new.
            var decision = AgentSelector.SelectForEscalation(Marcus.Id, Team, BillingCategory);

            Assert.Equal(Marcus.Id, decision.Agent?.Id);
        }

        [Fact]
        public void An_agent_over_their_limit_does_not_keep_it()
        {
            var overloaded = Agent(5, "Overloaded", true, 8, 6, Billing);

            var decision = AgentSelector.SelectForEscalation(5, [overloaded, Sara], BillingCategory);

            Assert.Equal(Sara.Id, decision.Agent?.Id);
        }

        [Fact]
        public void An_agent_who_became_inactive_is_replaced_and_the_reason_says_so()
        {
            var away = Agent(2, "Ben Osei", false, 3, 8, Technical, Outage);

            var decision = AgentSelector.SelectForEscalation(away.Id, [away, Priya], OutageCategory);

            Assert.Equal(Priya.Id, decision.Agent?.Id);
            Assert.StartsWith("Ben Osei is no longer eligible.", decision.Reason);
        }

        [Fact]
        public void An_agent_without_the_specialisation_is_replaced()
        {
            var decision = AgentSelector.SelectForEscalation(Alex.Id, Team, SecurityCategory);

            Assert.Equal(Priya.Id, decision.Agent?.Id);
        }

        [Fact]
        public void When_the_current_agent_is_out_and_nobody_else_is_eligible_the_ticket_is_unassigned()
        {
            var decision = AgentSelector.SelectForEscalation(Alex.Id, [Alex], SecurityCategory);

            Assert.Null(decision.Agent);
            Assert.Contains("Alex Turner is no longer eligible.", decision.Reason);
            Assert.Contains("No active agent specialises in Security.", decision.Reason);
        }

        [Fact]
        public void An_unassigned_ticket_is_assigned_afresh()
        {
            var decision = AgentSelector.SelectForEscalation(null, Team, BillingCategory);

            Assert.Equal(Sara.Id, decision.Agent?.Id);
        }

        [Fact]
        public void An_agent_who_no_longer_exists_is_treated_as_unassigned()
        {
            var decision = AgentSelector.SelectForEscalation(999, Team, BillingCategory);

            Assert.Equal(Sara.Id, decision.Agent?.Id);
        }
    }

    private static CategoryRequirement CategoryFor(int id) => id switch
    {
        General => GeneralCategory,
        Billing => BillingCategory,
        Technical => TechnicalCategory,
        Outage => OutageCategory,
        _ => SecurityCategory
    };

    private static AgentCandidate Agent(
        int id, string name, bool active, int open, int limit, params int[] specialisations) =>
        new(id, name, active, limit, open, specialisations);
}
