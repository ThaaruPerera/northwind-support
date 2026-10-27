using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using SupportDesk.Application.Authentication;
using SupportDesk.Infrastructure.Services;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Infrastructure.Services;

public class JwtAccessTokenIssuerTests
{
    private const string Key = "a-test-signing-key-of-at-least-32-bytes!";

    private static JwtSettings Settings(int lifetimeMinutes = 480, string key = Key) => new()
    {
        SigningKey = key,
        Issuer = "NorthwindSupport",
        Audience = "NorthwindSupport.Web",
        LifetimeMinutes = lifetimeMinutes
    };

    private static AccessTokenResult Issue(JwtSettings settings)
    {
        var clock = new FixedClock();
        var token = new JwtAccessTokenIssuer(settings, clock).Issue(4, "Sara Lindqvist", "sara@support.test");

        return new AccessTokenResult(token.Token, token.ExpiresAtUtc, clock.UtcNow, new JsonWebToken(token.Token));
    }

    private sealed record AccessTokenResult(string Raw, DateTime ExpiresAtUtc, DateTime IssuedAtUtc, JsonWebToken Parsed);

    [Fact]
    public void The_token_says_who_it_is_for()
    {
        var token = Issue(Settings()).Parsed;

        Assert.Equal("4", token.GetClaim("sub").Value);
        Assert.Equal("Sara Lindqvist", token.GetClaim("name").Value);
        Assert.Equal("sara@support.test", token.GetClaim("email").Value);
    }

    [Fact]
    public void The_token_names_its_issuer_and_audience_from_the_settings()
    {
        var token = Issue(Settings()).Parsed;

        Assert.Equal("NorthwindSupport", token.Issuer);
        Assert.Contains("NorthwindSupport.Web", token.Audiences);
    }

    [Fact]
    public void The_token_expires_after_the_configured_lifetime()
    {
        var issued = Issue(Settings(lifetimeMinutes: 480));

        Assert.Equal(issued.IssuedAtUtc.AddMinutes(480), issued.ExpiresAtUtc);
        Assert.Equal(issued.ExpiresAtUtc, issued.Parsed.ValidTo);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(60)]
    [InlineData(1440)]
    public void Changing_the_setting_changes_the_expiry_with_no_code_change(int minutes)
    {
        var issued = Issue(Settings(lifetimeMinutes: minutes));

        Assert.Equal(issued.IssuedAtUtc.AddMinutes(minutes), issued.ExpiresAtUtc);
    }

    [Fact]
    public void The_response_and_the_token_agree_on_the_moment_it_expires_to_the_second()
    {
        var issued = Issue(Settings());

        Assert.Equal(0, issued.ExpiresAtUtc.Ticks % TimeSpan.TicksPerSecond);
        Assert.Equal(issued.ExpiresAtUtc, issued.Parsed.ValidTo);
    }

    [Fact]
    public async Task The_token_is_signed_with_the_key_and_no_other()
    {
        var token = Issue(Settings()).Raw;

        Assert.True(await IsValid(token, Key));
        Assert.False(await IsValid(token, "a-different-key-that-is-also-32-bytes-long!"));
    }

    [Fact]
    public void Two_tokens_for_the_same_agent_are_different_tokens()
    {
        var first = Issue(Settings()).Raw;
        var second = Issue(Settings()).Raw;

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void The_token_does_not_contain_the_signing_key_or_a_password()
    {
        var token = Issue(Settings()).Parsed;

        Assert.DoesNotContain(Key, token.EncodedPayload);
        Assert.False(token.TryGetClaim("password", out _));
    }

    private static async Task<bool> IsValid(string token, string key)
    {
        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token, new TokenValidationParameters
        {
            ValidIssuer = "NorthwindSupport",
            ValidAudience = "NorthwindSupport.Web",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            // The test clock is fixed in the past, so the lifetime is checked by the tests above.
            ValidateLifetime = false
        });

        return result.IsValid;
    }
}
