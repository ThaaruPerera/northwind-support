using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Infrastructure.Data;
using SupportDesk.Infrastructure.Services;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Infrastructure.Data;

/// <summary>The development credentials: who gets one, what is stored, and that it is safe to repeat.</summary>
public sealed class SupportDbSeederCredentialsTests : IDisposable
{
    private readonly SqliteTestDatabase _database = new();
    private readonly IdentityPasswordHasher _hasher = new();
    private readonly SupportDbSeeder _seeder;

    public SupportDbSeederCredentialsTests()
    {
        _seeder = new SupportDbSeeder(Db, new FixedClock(), _hasher, NullLogger<SupportDbSeeder>.Instance);
    }

    private SupportDbContext Db => _database.Db;

    public void Dispose() => _database.Dispose();

    private Agent AddAgent(string name, bool active = true)
    {
        var agent = new Agent(name, $"{name.Replace(' ', '.').ToLowerInvariant()}@support.test", 10, FixedClock.DefaultNow);

        if (!active)
        {
            agent.Deactivate();
        }

        Db.Add(agent);
        Db.SaveChanges();

        return agent;
    }

    private async Task<List<Agent>> AllAgents()
    {
        Db.ChangeTracker.Clear();

        return await Db.Set<Agent>().AsNoTracking().OrderBy(a => a.Id).ToListAsync();
    }

    [Fact]
    public async Task Every_agent_without_a_password_gets_one_that_the_development_password_opens()
    {
        AddAgent("Alex Turner");
        AddAgent("Yuki Tanaka", active: false);

        await _seeder.SeedCredentialsAsync();

        foreach (var agent in await AllAgents())
        {
            Assert.NotNull(agent.PasswordHash);
            Assert.True(_hasher.Verify(agent.PasswordHash, SupportDbSeeder.DevelopmentPassword));
        }
    }

    [Fact]
    public async Task An_inactive_agent_gets_credentials_too_so_that_being_refused_is_about_being_inactive()
    {
        AddAgent("Yuki Tanaka", active: false);

        await _seeder.SeedCredentialsAsync();

        var yuki = Assert.Single(await AllAgents());
        Assert.NotNull(yuki.PasswordHash);
        Assert.False(yuki.IsActive);
    }

    [Fact]
    public async Task The_password_itself_is_never_stored()
    {
        AddAgent("Alex Turner");

        await _seeder.SeedCredentialsAsync();

        var stored = Assert.Single(await AllAgents()).PasswordHash;
        Assert.DoesNotContain(SupportDbSeeder.DevelopmentPassword, stored);
        Assert.NotEqual(SupportDbSeeder.DevelopmentPassword, stored);
    }

    [Fact]
    public async Task Each_agent_has_their_own_salt_so_the_same_password_gives_different_hashes()
    {
        AddAgent("Alex Turner");
        AddAgent("Ben Osei");

        await _seeder.SeedCredentialsAsync();

        var hashes = (await AllAgents()).Select(a => a.PasswordHash).ToList();
        Assert.Equal(2, hashes.Distinct().Count());
    }

    [Fact]
    public async Task An_agent_who_already_has_a_password_keeps_it()
    {
        var alex = AddAgent("Alex Turner");
        var own = _hasher.Hash("Their_Own_Password_1");
        alex.SetPasswordHash(own);
        Db.SaveChanges();
        AddAgent("Ben Osei");

        await _seeder.SeedCredentialsAsync();

        var agents = await AllAgents();
        Assert.Equal(own, agents[0].PasswordHash);
        Assert.False(_hasher.Verify(agents[0].PasswordHash!, SupportDbSeeder.DevelopmentPassword));
        Assert.True(_hasher.Verify(agents[1].PasswordHash!, SupportDbSeeder.DevelopmentPassword));
    }

    [Fact]
    public async Task Running_it_again_changes_nothing()
    {
        AddAgent("Alex Turner");
        AddAgent("Ben Osei");
        await _seeder.SeedCredentialsAsync();
        var before = (await AllAgents()).Select(a => a.PasswordHash).ToList();

        await _seeder.SeedCredentialsAsync();

        Assert.Equal(before, (await AllAgents()).Select(a => a.PasswordHash).ToList());
    }

    [Fact]
    public async Task With_no_agents_it_does_nothing_and_does_not_fail()
    {
        await _seeder.SeedCredentialsAsync();

        Assert.Empty(await AllAgents());
    }
}
