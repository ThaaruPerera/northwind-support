using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Domain.Aggregates.Tickets;

public class SlaPolicyTests
{
    private readonly SlaPolicy _policy = new(SlaTestSettings.Default());

    [Theory]
    [InlineData(TicketPriority.Critical, 4)]
    [InlineData(TicketPriority.High, 8)]
    [InlineData(TicketPriority.Medium, 24)]
    [InlineData(TicketPriority.Low, 72)]
    public void A_standard_customer_gets_the_base_window(TicketPriority priority, double hours)
    {
        var window = _policy.WindowFor(priority, CustomerTier.Standard);

        Assert.Equal(TimeSpan.FromHours(hours), window.Duration);
    }

    [Theory]
    [InlineData(TicketPriority.Critical, 2)]
    [InlineData(TicketPriority.High, 4)]
    [InlineData(TicketPriority.Medium, 12)]
    [InlineData(TicketPriority.Low, 36)]
    public void A_premium_customer_gets_half_the_window(TicketPriority priority, double hours)
    {
        var window = _policy.WindowFor(priority, CustomerTier.Premium);

        Assert.Equal(TimeSpan.FromHours(hours), window.Duration);
    }

    [Fact]
    public void A_premium_window_is_never_shorter_than_the_minimum()
    {
        var settings = SlaTestSettings.Default();
        settings.BaseWindowHours[TicketPriority.Critical] = 1.5;

        var window = new SlaPolicy(settings).WindowFor(TicketPriority.Critical, CustomerTier.Premium);

        Assert.Equal(TimeSpan.FromHours(1), window.Duration);
        Assert.Contains("minimum", window.Reason);
    }

    [Fact]
    public void A_premium_window_exactly_at_the_minimum_is_not_raised()
    {
        var settings = SlaTestSettings.Default();
        settings.BaseWindowHours[TicketPriority.Critical] = 2;

        var window = new SlaPolicy(settings).WindowFor(TicketPriority.Critical, CustomerTier.Premium);

        Assert.Equal(TimeSpan.FromHours(1), window.Duration);
        Assert.DoesNotContain("minimum", window.Reason);
    }

    [Fact]
    public void The_numbers_come_from_the_settings_not_from_the_code()
    {
        var settings = SlaTestSettings.Default();
        settings.BaseWindowHours[TicketPriority.Medium] = 10;
        settings.PremiumCustomerMultiplier = 0.25;

        var policy = new SlaPolicy(settings);

        Assert.Equal(TimeSpan.FromHours(10), policy.WindowFor(TicketPriority.Medium, CustomerTier.Standard).Duration);
        Assert.Equal(TimeSpan.FromHours(2.5), policy.WindowFor(TicketPriority.Medium, CustomerTier.Premium).Duration);
    }

    [Fact]
    public void The_reason_names_the_priority_and_the_tier_effect()
    {
        var standard = _policy.WindowFor(TicketPriority.High, CustomerTier.Standard);
        var premium = _policy.WindowFor(TicketPriority.High, CustomerTier.Premium);

        Assert.Equal("High priority has a base window of 8h.", standard.Reason);
        Assert.Equal("High priority has a base window of 8h; a Premium customer gets 4h.", premium.Reason);
    }

    [Fact]
    public void The_due_date_is_the_start_plus_the_window()
    {
        var window = _policy.WindowFor(TicketPriority.High, CustomerTier.Standard);

        Assert.Equal(FixedClock.DefaultNow.AddHours(8), SlaPolicy.DueAt(FixedClock.DefaultNow, window));
    }
}
