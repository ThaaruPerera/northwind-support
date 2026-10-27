using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Domain.Aggregates.Tickets;

public class SlaEvaluatorTests
{
    private static readonly DateTime Now = FixedClock.DefaultNow;

    [Fact]
    public void Evaluate_WithoutADueDate_IsNotApplicable() =>
        Assert.Equal(SlaStatus.NotApplicable, SlaEvaluator.Evaluate(null, null, Now));

    [Fact]
    public void Evaluate_ResolvedOnTime_IsMet() =>
        Assert.Equal(SlaStatus.Met, SlaEvaluator.Evaluate(Now, Now, Now.AddDays(1)));

    [Fact]
    public void Evaluate_ResolvedLate_IsBreached() =>
        Assert.Equal(SlaStatus.Breached, SlaEvaluator.Evaluate(Now, Now.AddMinutes(1), Now));

    [Fact]
    public void Evaluate_OpenAndPastDue_IsBreached() =>
        Assert.Equal(SlaStatus.Breached, SlaEvaluator.Evaluate(Now.AddMinutes(-1), null, Now));

    [Fact]
    public void Evaluate_OpenAndBeforeDue_IsWithinSla() =>
        Assert.Equal(SlaStatus.WithinSla, SlaEvaluator.Evaluate(Now.AddHours(1), null, Now));

    /// <summary>An 8 hour window that started at <see cref="Now"/>; 25% of it is the last 2 hours.</summary>
    public class WithAtRisk
    {
        private static readonly DateTime Started = FixedClock.DefaultNow;
        private static readonly DateTime Due = Started.AddHours(8);
        private const decimal Threshold = 25;

        private static SlaStatus Open(DateTime now) => SlaEvaluator.Evaluate(Due, null, now, Started, Threshold);

        [Fact]
        public void Without_a_due_date_it_is_not_applicable() =>
            Assert.Equal(SlaStatus.NotApplicable,
                SlaEvaluator.Evaluate(null, null, Started, Started, Threshold));

        [Fact]
        public void Plenty_of_time_left_is_within_sla() =>
            Assert.Equal(SlaStatus.WithinSla, Open(Started.AddHours(1)));

        [Fact]
        public void Just_over_the_threshold_is_still_within_sla() =>
            Assert.Equal(SlaStatus.WithinSla, Open(Due.AddHours(-2).AddTicks(-1)));

        [Fact]
        public void Exactly_at_the_threshold_is_at_risk() =>
            Assert.Equal(SlaStatus.AtRisk, Open(Due.AddHours(-2)));

        [Fact]
        public void Just_under_the_threshold_is_at_risk() =>
            Assert.Equal(SlaStatus.AtRisk, Open(Due.AddHours(-2).AddTicks(1)));

        [Fact]
        public void Exactly_at_the_due_date_is_at_risk_not_breached() =>
            Assert.Equal(SlaStatus.AtRisk, Open(Due));

        [Fact]
        public void One_tick_after_the_due_date_is_breached() =>
            Assert.Equal(SlaStatus.Breached, Open(Due.AddTicks(1)));

        [Fact]
        public void Resolved_on_time_is_met_even_inside_the_at_risk_zone() =>
            Assert.Equal(SlaStatus.Met,
                SlaEvaluator.Evaluate(Due, Due.AddMinutes(-30), Due, Started, Threshold));

        [Fact]
        public void Resolved_late_is_breached() =>
            Assert.Equal(SlaStatus.Breached,
                SlaEvaluator.Evaluate(Due, Due.AddTicks(1), Due.AddDays(1), Started, Threshold));

        [Fact]
        public void The_window_is_measured_from_the_clock_start_not_from_creation()
        {
            // Escalated at hour 7 of a ticket created at hour 0: a fresh 4 hour window, due at hour 11.
            var restarted = Started.AddHours(7);
            var due = restarted.AddHours(4);

            // One hour after the restart, 3 of 4 hours remain: comfortable.
            Assert.Equal(SlaStatus.WithinSla,
                SlaEvaluator.Evaluate(due, null, restarted.AddHours(1), restarted, Threshold));

            // With 1 hour left (25%), it is at risk.
            Assert.Equal(SlaStatus.AtRisk,
                SlaEvaluator.Evaluate(due, null, due.AddHours(-1), restarted, Threshold));
        }

        [Fact]
        public void A_threshold_of_zero_never_marks_a_ticket_at_risk_before_it_is_due() =>
            Assert.Equal(SlaStatus.WithinSla,
                SlaEvaluator.Evaluate(Due, null, Due.AddTicks(-1), Started, 0));
    }
}
