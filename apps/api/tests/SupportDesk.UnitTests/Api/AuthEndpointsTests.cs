using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.UnitTests.Api;

/// <summary>Signing in and out of the real application, over HTTP.</summary>
public class AuthEndpointsTests(ApiFixture api) : IClassFixture<ApiFixture>
{
    private const string Password = SupportDbSeeder.DevelopmentPassword;

    private static LoginRequest Login(string email, string password = Password) => new(email, password);

    private async Task<HttpResponseMessage> PostLogin(string email, string password = Password)
    {
        using var client = api.Client();

        return await client.PostAsJsonAsync("/api/auth/login", Login(email, password));
    }

    [Fact]
    public async Task The_right_email_and_password_give_a_token_and_the_agent()
    {
        var response = await PostLogin(ApiFixture.AlexEmail);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var login = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.False(string.IsNullOrWhiteSpace(login!.Token));
        Assert.Equal("Alex Turner", login.User.FullName);
        Assert.True(login.ExpiresAtUtc > DateTime.UtcNow.AddHours(7));
    }

    [Fact]
    public async Task A_wrong_password_gives_401_and_no_token()
    {
        var response = await PostLogin(ApiFixture.AlexEmail, "wrong password");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("token", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Invalid email or password.", body);
    }

    [Fact]
    public async Task A_wrong_email_a_wrong_password_and_an_inactive_agent_are_told_exactly_the_same_thing()
    {
        var wrongPassword = await PostLogin(ApiFixture.AlexEmail, "wrong password");
        var unknownEmail = await PostLogin("nobody@northwind-support.example");
        var inactive = await PostLogin("yuki.tanaka@northwind-support.example");

        foreach (var response in new[] { wrongPassword, unknownEmail, inactive })
        {
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        var messages = new List<string?>();

        foreach (var response in new[] { wrongPassword, unknownEmail, inactive })
        {
            var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
            messages.Add(problem.GetProperty("detail").GetString());
        }

        Assert.Single(messages.Distinct());
        Assert.Equal("Invalid email or password.", messages[0]);
    }

    [Theory]
    [InlineData("ALEX.TURNER@NORTHWIND-SUPPORT.EXAMPLE")]
    [InlineData("  Alex.Turner@Northwind-Support.Example  ")]
    public async Task The_email_is_matched_whatever_its_case_and_spacing(string typed)
    {
        var response = await PostLogin(typed);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task A_form_with_nothing_in_it_is_a_400_and_not_a_401()
    {
        using var client = api.Client();

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("", ""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Me_is_the_signed_in_agent_with_a_token_and_401_without_one()
    {
        var token = await api.SignInAsync(ApiFixture.AlexEmail);

        using var signedIn = api.Client(token);
        var me = await signedIn.GetFromJsonAsync<CurrentAgentDto>("/api/auth/me");

        using var anonymous = api.Client();
        var refused = await anonymous.GetAsync("/api/auth/me");

        Assert.Equal("Alex Turner", me!.FullName);
        Assert.Equal(HttpStatusCode.Unauthorized, refused.StatusCode);
    }

    [Fact]
    public async Task An_agent_deactivated_after_signing_in_is_turned_away_by_me_and_cannot_escalate()
    {
        // Marcus is used only here, so deactivating him cannot disturb another test.
        var token = await api.SignInAsync("marcus.doyle@northwind-support.example");
        await api.WithDatabaseAsync(async db =>
            await db.Set<Agent>().Where(a => a.Email == "marcus.doyle@northwind-support.example")
                .ExecuteUpdateAsync(set => set.SetProperty(a => a.IsActive, false)));

        using var client = api.Client(token);
        var me = await client.GetAsync("/api/auth/me");
        var escalate = await client.PostAsJsonAsync("/api/tickets/1/escalate", new EscalateTicketRequest("Customer is waiting"));

        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, escalate.StatusCode);
    }

    [Fact]
    public async Task A_name_sent_in_the_escalate_request_is_ignored_and_the_signed_in_agent_is_recorded()
    {
        var token = await api.SignInAsync(ApiFixture.AlexEmail);
        using var client = api.Client(token);

        // The request is written by hand so that it can carry a field the API no longer accepts.
        var response = await client.PostAsync(
            "/api/tickets/2/escalate",
            JsonContent.Create(new { reason = "Customer is waiting", escalatedBy = "Mallory the Impostor" }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Alex Turner", result.GetProperty("escalation").GetProperty("escalatedBy").GetString());

        var history = await client.GetFromJsonAsync<JsonElement>("/api/tickets/2/escalations");
        Assert.Equal("Alex Turner", history[0].GetProperty("escalatedBy").GetString());
        Assert.DoesNotContain("Mallory", history.GetRawText());
    }

    [Fact]
    public async Task Each_agent_is_recorded_under_their_own_name()
    {
        var ben = await api.SignInAsync("ben.osei@northwind-support.example");
        using var client = api.Client(ben);

        var response = await client.PostAsJsonAsync("/api/tickets/4/escalate", new EscalateTicketRequest("Customer is waiting"));

        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Ben Osei", result.GetProperty("escalation").GetProperty("escalatedBy").GetString());
    }

    [Fact]
    public async Task An_unknown_ticket_is_a_404_for_a_signed_in_agent_and_a_401_for_a_stranger()
    {
        var token = await api.SignInAsync(ApiFixture.AlexEmail);
        using var signedIn = api.Client(token);
        using var stranger = api.Client();
        var body = new EscalateTicketRequest("Customer is waiting");

        var asAgent = await signedIn.PostAsJsonAsync("/api/tickets/9999/escalate", body);
        var asStranger = await stranger.PostAsJsonAsync("/api/tickets/9999/escalate", body);

        Assert.Equal(HttpStatusCode.NotFound, asAgent.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, asStranger.StatusCode);
    }
}
