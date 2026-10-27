using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SupportDesk.Application.Authentication;
using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;
using SupportDesk.Infrastructure.Services;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Api;

/// <summary>
/// The application started as a container starts it: Production, no development settings file, and everything
/// secret handed in from outside. Shows what a deployment does and, as important, what it does not do.
/// </summary>
public class ProductionStartupTests
{
    /// <summary>What appsettings.Development.json holds. It is not in a published build, so it must not work there.</summary>
    private const string DevelopmentSigningKey = "local-development-key-not-a-secret-never-use-elsewhere";

    private static async Task<T> WithProduction<T>(ApiFixture fixture, Func<ApiFixture, Task<T>> run)
    {
        await fixture.InitializeAsync();

        try
        {
            return await run(fixture);
        }
        finally
        {
            await fixture.DisposeAsync();
        }
    }

    [Fact]
    public async Task It_starts_from_the_published_settings_and_what_the_environment_provides()
    {
        var result = await WithProduction(ApiFixture.ProductionWithoutDemoData(), async api =>
        {
            using var client = api.Client();
            var health = await client.GetAsync("/health");

            return (
                Health: health.StatusCode,
                Sla: api.Services.GetRequiredService<SlaSettings>(),
                Jwt: api.Services.GetRequiredService<JwtSettings>());
        });

        Assert.Equal(HttpStatusCode.OK, result.Health);
        Assert.Equal(4, result.Sla.BaseWindowHours[TicketPriority.Critical]);
        Assert.Equal(480, result.Jwt.LifetimeMinutes);
        Assert.Equal(ApiFixture.ProductionSigningKey, result.Jwt.SigningKey);
    }

    [Fact]
    public async Task A_token_signed_with_the_development_key_is_refused()
    {
        var status = await WithProduction(ApiFixture.ProductionWithoutDemoData(), async api =>
        {
            var real = api.Services.GetRequiredService<JwtSettings>();
            var forged = new JwtSettings
            {
                SigningKey = DevelopmentSigningKey,
                Issuer = real.Issuer,
                Audience = real.Audience,
                LifetimeMinutes = real.LifetimeMinutes
            };
            var token = new JwtAccessTokenIssuer(forged, new SystemClock()).Issue(1, "Alex Turner", ApiFixture.AlexEmail).Token;

            using var client = api.Client(token);

            return (await client.GetAsync("/api/tickets")).StatusCode;
        });

        Assert.Equal(HttpStatusCode.Unauthorized, status);
    }

    [Fact]
    public async Task Swagger_is_not_served_even_to_someone_who_is_signed_in()
    {
        var status = await WithProduction(ApiFixture.ProductionWithoutDemoData(), async api =>
        {
            // Nobody is seeded here, so a valid token is made directly with the key this start was given.
            var real = api.Services.GetRequiredService<JwtSettings>();
            var token = new JwtAccessTokenIssuer(real, new SystemClock()).Issue(1, "Alex Turner", ApiFixture.AlexEmail).Token;

            using var client = api.Client(token);

            return (await client.GetAsync("/swagger/v1/swagger.json")).StatusCode;
        });

        Assert.Equal(HttpStatusCode.NotFound, status);
    }

    [Fact]
    public async Task A_stranger_cannot_tell_which_paths_exist_because_every_unknown_path_asks_them_to_sign_in()
    {
        var statuses = await WithProduction(ApiFixture.ProductionWithoutDemoData(), async api =>
        {
            using var client = api.Client();

            return new[]
            {
                (await client.GetAsync("/swagger/v1/swagger.json")).StatusCode,
                (await client.GetAsync("/no/such/page")).StatusCode,
                (await client.GetAsync("/api/tickets")).StatusCode
            };
        });

        Assert.All(statuses, status => Assert.Equal(HttpStatusCode.Unauthorized, status));
    }

    [Fact]
    public async Task Nothing_is_seeded_and_so_nobody_has_the_development_password()
    {
        var result = await WithProduction(ApiFixture.ProductionWithoutDemoData(), async api =>
        {
            var agents = 0;
            await api.WithDatabaseAsync(async db => agents = await db.Set<Agent>().CountAsync());

            using var client = api.Client();
            var login = await client.PostAsJsonAsync(
                "/api/auth/login", new LoginRequest(ApiFixture.AlexEmail, SupportDbSeeder.DevelopmentPassword));

            return (Agents: agents, Login: login.StatusCode);
        });

        Assert.Equal(0, result.Agents);
        Assert.Equal(HttpStatusCode.Unauthorized, result.Login);
    }

    [Fact]
    public async Task Demo_data_and_the_development_password_appear_only_when_seeding_is_switched_on()
    {
        var result = await WithProduction(ApiFixture.ProductionSeedingDemoData(), async api =>
        {
            var tickets = 0;
            await api.WithDatabaseAsync(async db => tickets = await db.Set<Ticket>().CountAsync());

            var token = await api.SignInAsync(ApiFixture.AlexEmail);

            return (Tickets: tickets, Token: token);
        });

        Assert.Equal(40, result.Tickets);
        Assert.False(string.IsNullOrWhiteSpace(result.Token));
    }
}
