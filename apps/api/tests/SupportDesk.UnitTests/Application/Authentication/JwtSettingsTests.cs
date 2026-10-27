using SupportDesk.Application.Authentication;

namespace SupportDesk.UnitTests.Application.Authentication;

public class JwtSettingsTests
{
    private static JwtSettings Valid() => new()
    {
        SigningKey = new string('k', 32),
        Issuer = "NorthwindSupport",
        Audience = "NorthwindSupport.Web",
        LifetimeMinutes = 480
    };

    [Fact]
    public void Sensible_settings_are_accepted() => Valid().Validate();

    [Fact]
    public void The_lifetime_is_the_configured_number_of_minutes()
    {
        var settings = Valid();
        settings.LifetimeMinutes = 90;

        Assert.Equal(TimeSpan.FromMinutes(90), settings.Lifetime);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void A_missing_key_stops_startup_and_says_how_to_provide_it(string? key)
    {
        var settings = Valid();
        settings.SigningKey = key!;

        var error = Assert.Throws<InvalidOperationException>(settings.Validate);

        Assert.Contains("Jwt__SigningKey", error.Message);
    }

    [Fact]
    public void A_key_under_256_bits_is_refused()
    {
        var settings = Valid();
        settings.SigningKey = new string('k', 31);

        Assert.Throws<InvalidOperationException>(settings.Validate);
    }

    [Fact]
    public void A_key_of_exactly_256_bits_is_accepted()
    {
        var settings = Valid();
        settings.SigningKey = new string('k', 32);

        settings.Validate();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(1441)]
    public void A_lifetime_outside_one_minute_to_one_day_is_refused(int minutes)
    {
        var settings = Valid();
        settings.LifetimeMinutes = minutes;

        var error = Assert.Throws<InvalidOperationException>(settings.Validate);

        Assert.Contains("LifetimeMinutes", error.Message);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(1440)]
    public void The_ends_of_the_allowed_lifetime_are_accepted(int minutes)
    {
        var settings = Valid();
        settings.LifetimeMinutes = minutes;

        settings.Validate();
    }

    [Fact]
    public void A_missing_issuer_or_audience_is_refused()
    {
        var noIssuer = Valid();
        noIssuer.Issuer = "";
        var noAudience = Valid();
        noAudience.Audience = " ";

        Assert.Throws<InvalidOperationException>(noIssuer.Validate);
        Assert.Throws<InvalidOperationException>(noAudience.Validate);
    }

    [Fact]
    public void Every_problem_is_reported_at_once()
    {
        var error = Assert.Throws<InvalidOperationException>(() => new JwtSettings().Validate());

        Assert.Contains("SigningKey", error.Message);
        Assert.Contains("Issuer", error.Message);
        Assert.Contains("Audience", error.Message);
        Assert.Contains("LifetimeMinutes", error.Message);
    }
}
