using Microsoft.Extensions.Logging.Abstractions;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Auth.Commands.Login;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Repositories;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Application.Features.Auth;

public class LoginCommandHandlerTests
{
    private const string Hash = "stored-hash";
    private const string DummyHash = "dummy-hash";
    private static readonly AccessToken Token = new("the.jwt.token", FixedClock.DefaultNow.AddHours(8));

    private readonly Mock<IAgentRepository> _agents = new();
    private readonly Mock<IPasswordHasher> _hasher = new();
    private readonly Mock<IAccessTokenIssuer> _tokens = new();
    private readonly LoginCommandHandler _handler;

    public LoginCommandHandlerTests()
    {
        _hasher.SetupGet(h => h.DummyHash).Returns(DummyHash);
        _hasher.Setup(h => h.Verify(Hash, "right")).Returns(true);
        _tokens.Setup(t => t.Issue(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>())).Returns(Token);

        _handler = new LoginCommandHandler(_agents.Object, _hasher.Object, _tokens.Object, NullLogger<LoginCommandHandler>.Instance);
    }

    private static Agent Alex(bool active = true, string? passwordHash = Hash)
    {
        var agent = new Agent("Alex Turner", "alex@support.test", 10, FixedClock.DefaultNow);

        if (passwordHash is not null)
        {
            agent.SetPasswordHash(passwordHash);
        }

        if (!active)
        {
            agent.Deactivate();
        }

        return agent;
    }

    private void KnownAgent(Agent? agent) =>
        _agents.Setup(a => a.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(agent);

    private Task<LoginResponse> SignIn(string email = "alex@support.test", string password = "right") =>
        _handler.HandleAsync(new LoginRequest(email, password), CancellationToken.None);

    [Fact]
    public async Task The_right_email_and_password_give_a_token_and_the_agents_details()
    {
        KnownAgent(Alex());

        var response = await SignIn();

        Assert.Equal("the.jwt.token", response.Token);
        Assert.Equal(Token.ExpiresAtUtc, response.ExpiresAtUtc);
        Assert.Equal("Alex Turner", response.User.FullName);
        Assert.Equal("alex@support.test", response.User.Email);
    }

    [Fact]
    public async Task The_email_is_handed_to_the_lookup_as_typed_which_ignores_case_and_spaces()
    {
        KnownAgent(Alex());

        await SignIn(email: "  ALEX@Support.Test ");

        _agents.Verify(a => a.GetByEmailAsync("  ALEX@Support.Test ", It.IsAny<CancellationToken>()), Times.Once);
    }

    public static IEnumerable<object[]> Refusals()
    {
        yield return ["unknown email"];
        yield return ["wrong password"];
        yield return ["inactive agent"];
        yield return ["no password set"];
    }

    private void Arrange(string refusal) => KnownAgent(refusal switch
    {
        "unknown email" => null,
        "inactive agent" => Alex(active: false),
        "no password set" => Alex(passwordHash: null),
        _ => Alex()
    });

    private string PasswordFor(string refusal) => refusal == "wrong password" ? "wrong" : "right";

    [Theory]
    [MemberData(nameof(Refusals))]
    public async Task Every_way_of_being_refused_gives_the_same_failure_with_the_same_message(string refusal)
    {
        Arrange(refusal);

        var error = await Assert.ThrowsAsync<AuthenticationFailedException>(() => SignIn(password: PasswordFor(refusal)));

        Assert.Equal("Invalid email or password.", error.Message);
        Assert.Equal(LoginCommandHandler.FailureMessage, error.Message);
    }

    [Theory]
    [MemberData(nameof(Refusals))]
    public async Task A_refused_sign_in_never_gets_a_token(string refusal)
    {
        Arrange(refusal);

        await Assert.ThrowsAsync<AuthenticationFailedException>(() => SignIn(password: PasswordFor(refusal)));

        _tokens.Verify(t => t.Issue(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Theory]
    [MemberData(nameof(Refusals))]
    public async Task Exactly_one_password_check_is_made_whatever_the_reason_so_every_refusal_takes_about_as_long(string refusal)
    {
        Arrange(refusal);

        await Assert.ThrowsAsync<AuthenticationFailedException>(() => SignIn(password: PasswordFor(refusal)));

        _hasher.Verify(h => h.Verify(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task An_unknown_email_is_checked_against_the_dummy_hash()
    {
        KnownAgent(null);

        await Assert.ThrowsAsync<AuthenticationFailedException>(() => SignIn(password: "anything"));

        _hasher.Verify(h => h.Verify(DummyHash, "anything"), Times.Once);
    }

    [Fact]
    public async Task An_agent_with_no_password_is_checked_against_the_dummy_hash_too()
    {
        KnownAgent(Alex(passwordHash: null));

        await Assert.ThrowsAsync<AuthenticationFailedException>(() => SignIn());

        _hasher.Verify(h => h.Verify(DummyHash, "right"), Times.Once);
    }

    [Fact]
    public async Task A_known_agent_is_checked_against_their_own_hash()
    {
        KnownAgent(Alex());

        await SignIn();

        _hasher.Verify(h => h.Verify(Hash, "right"), Times.Once);
    }

    [Fact]
    public async Task An_inactive_agent_with_the_right_password_is_still_refused()
    {
        KnownAgent(Alex(active: false));

        await Assert.ThrowsAsync<AuthenticationFailedException>(() => SignIn(password: "right"));
    }
}
