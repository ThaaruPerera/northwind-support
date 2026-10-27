using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Domain.Aggregates.Tickets;

public class SlaSettingsTests
{
    [Fact]
    public void The_shipped_numbers_are_valid() => SlaTestSettings.Default().Validate();

    [Fact]
    public void A_missing_priority_is_rejected()
    {
        var settings = SlaTestSettings.Default();
        settings.BaseWindowHours.Remove(TicketPriority.High);

        var error = Assert.Throws<InvalidOperationException>(settings.Validate);

        Assert.Contains("High", error.Message);
    }

    [Fact]
    public void A_window_of_zero_is_rejected()
    {
        var settings = SlaTestSettings.Default();
        settings.BaseWindowHours[TicketPriority.Low] = 0;

        Assert.Throws<InvalidOperationException>(settings.Validate);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.5)]
    [InlineData(1.5)]
    public void A_premium_multiplier_outside_zero_to_one_is_rejected(double multiplier)
    {
        var settings = SlaTestSettings.Default();
        settings.PremiumCustomerMultiplier = multiplier;

        Assert.Throws<InvalidOperationException>(settings.Validate);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void A_threshold_outside_zero_to_one_hundred_is_rejected(int percent)
    {
        var settings = SlaTestSettings.Default();
        settings.AtRiskThresholdPercent = percent;

        Assert.Throws<InvalidOperationException>(settings.Validate);
    }

    [Fact]
    public void Every_problem_is_reported_at_once()
    {
        var settings = SlaTestSettings.Default();
        settings.MinimumWindowHours = 0;
        settings.AtRiskThresholdPercent = 500;

        var error = Assert.Throws<InvalidOperationException>(settings.Validate);

        Assert.Contains("MinimumWindowHours", error.Message);
        Assert.Contains("AtRiskThresholdPercent", error.Message);
    }
}
