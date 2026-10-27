using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Domain.Aggregates.Agents;

public class AgentTests
{
    private static Agent NewAgent() => new("Alex Turner", "alex@support.test", 10, FixedClock.DefaultNow);

    [Fact]
    public void A_new_agent_has_no_password_and_so_cannot_sign_in() =>
        Assert.Null(NewAgent().PasswordHash);

    [Fact]
    public void Setting_a_password_hash_stores_it()
    {
        var agent = NewAgent();

        agent.SetPasswordHash("a-hash");

        Assert.Equal("a-hash", agent.PasswordHash);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_blank_hash_is_refused_so_an_agent_is_never_left_with_an_empty_password(string blank)
    {
        var agent = NewAgent();

        Assert.Throws<ArgumentException>(() => agent.SetPasswordHash(blank));
        Assert.Null(agent.PasswordHash);
    }
}
