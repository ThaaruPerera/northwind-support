using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Auth.Queries.GetCurrentAgent;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Repositories;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Application.Features.Auth;

public class GetCurrentAgentQueryHandlerTests
{
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly Mock<IAgentRepository> _agents = new();
    private readonly GetCurrentAgentQueryHandler _handler;

    public GetCurrentAgentQueryHandlerTests()
    {
        _handler = new GetCurrentAgentQueryHandler(_currentUser.Object, _agents.Object);
    }

    private static Agent Alex(bool active = true)
    {
        var agent = new Agent("Alex Turner", "alex@support.test", 10, FixedClock.DefaultNow);

        if (!active)
        {
            agent.Deactivate();
        }

        return agent;
    }

    private void SignedInAs(int? id, Agent? agent)
    {
        _currentUser.SetupGet(c => c.AgentId).Returns(id);
        _agents.Setup(a => a.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(agent);
    }

    [Fact]
    public async Task It_returns_the_signed_in_agent()
    {
        SignedInAs(7, Alex());

        var agent = await _handler.HandleAsync(CancellationToken.None);

        Assert.Equal("Alex Turner", agent.FullName);
        Assert.Equal("alex@support.test", agent.Email);
        _agents.Verify(a => a.GetByIdAsync(7, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Nobody_signed_in_is_refused_without_looking_anything_up()
    {
        SignedInAs(null, null);

        await Assert.ThrowsAsync<AuthenticationFailedException>(() => _handler.HandleAsync(CancellationToken.None));

        _agents.Verify(a => a.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task An_agent_who_has_been_deactivated_since_signing_in_is_refused_though_their_token_has_not_expired()
    {
        SignedInAs(7, Alex(active: false));

        await Assert.ThrowsAsync<AuthenticationFailedException>(() => _handler.HandleAsync(CancellationToken.None));
    }

    [Fact]
    public async Task An_agent_who_no_longer_exists_is_refused()
    {
        SignedInAs(7, null);

        await Assert.ThrowsAsync<AuthenticationFailedException>(() => _handler.HandleAsync(CancellationToken.None));
    }
}
