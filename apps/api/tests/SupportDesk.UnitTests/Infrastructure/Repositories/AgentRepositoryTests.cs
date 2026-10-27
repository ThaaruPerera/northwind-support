using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;
using SupportDesk.Infrastructure.Repositories;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Infrastructure.Repositories;

/// <summary>Runs the assignment candidate query against a real (in-memory SQLite) database.</summary>
public sealed class AgentRepositoryTests : IDisposable
{
    private static readonly DateTime Now = FixedClock.DefaultNow;

    private readonly SqliteTestDatabase _database = new();

    private SupportDbContext _db => _database.Db;

    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task Candidates_carry_the_open_ticket_count_the_limit_and_the_specialisations()
    {
        var billing = new Category("Billing", true, false);
        var general = new Category("General", false, false);
        var customer = new Customer("Fabrikam Inc", "a@fabrikam.test", null, CustomerTier.Standard, Now);
        var sara = new Agent("Sara Lindqvist", "sara@support.test", 6, Now);
        var yuki = new Agent("Yuki Tanaka", "yuki@support.test", 8, Now);
        yuki.Deactivate();

        _db.AddRange(billing, general, customer, sara, yuki);
        await _db.SaveChangesAsync();

        sara.AddSpecialization(billing.Id);
        sara.AddSpecialization(general.Id);

        // Two open tickets and two finished ones: only the open ones count.
        AddTicket("TCK-0001", customer, general, sara, TicketStatus.New);
        AddTicket("TCK-0002", customer, billing, sara, TicketStatus.InProgress);
        AddTicket("TCK-0003", customer, billing, sara, TicketStatus.Resolved);
        AddTicket("TCK-0004", customer, billing, sara, TicketStatus.Closed);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var candidates = await new AgentRepository(_db).GetAssignmentCandidatesAsync(default);

        var candidateSara = Assert.Single(candidates, c => c.FullName == "Sara Lindqvist");
        Assert.Equal(2, candidateSara.OpenTicketCount);
        Assert.Equal(6, candidateSara.MaxOpenTickets);
        Assert.True(candidateSara.IsActive);
        Assert.Equal(new[] { billing.Id, general.Id }.Order(), candidateSara.SpecializationCategoryIds.Order());

        var candidateYuki = Assert.Single(candidates, c => c.FullName == "Yuki Tanaka");
        Assert.False(candidateYuki.IsActive);
        Assert.Equal(0, candidateYuki.OpenTicketCount);
        Assert.Empty(candidateYuki.SpecializationCategoryIds);
    }

    [Theory]
    [InlineData("alex.turner@northwind-support.example")]
    [InlineData("ALEX.TURNER@NORTHWIND-SUPPORT.EXAMPLE")]
    [InlineData("Alex.Turner@Northwind-Support.Example")]
    [InlineData("  alex.turner@northwind-support.example  ")]
    public async Task An_email_is_found_whatever_its_case_and_spacing(string typed)
    {
        _db.Add(new Agent("Alex Turner", "alex.turner@northwind-support.example", 10, Now));
        await _db.SaveChangesAsync();

        var found = await new AgentRepository(_db).GetByEmailAsync(typed, default);

        Assert.Equal("Alex Turner", found?.FullName);
    }

    [Theory]
    [InlineData("someone.else@northwind-support.example")]
    [InlineData("alex.turner@northwind-support.exampl")]
    [InlineData("")]
    public async Task An_email_that_belongs_to_nobody_finds_nobody(string typed)
    {
        _db.Add(new Agent("Alex Turner", "alex.turner@northwind-support.example", 10, Now));
        await _db.SaveChangesAsync();

        Assert.Null(await new AgentRepository(_db).GetByEmailAsync(typed, default));
    }

    [Fact]
    public async Task The_stored_hash_comes_back_with_the_agent()
    {
        var agent = new Agent("Alex Turner", "alex.turner@northwind-support.example", 10, Now);
        agent.SetPasswordHash("a-stored-hash");
        _db.Add(agent);
        await _db.SaveChangesAsync();

        var found = await new AgentRepository(_db).GetByEmailAsync("alex.turner@northwind-support.example", default);

        Assert.Equal("a-stored-hash", found?.PasswordHash);
    }

    private void AddTicket(string reference, Customer customer, Category category, Agent agent, TicketStatus status)
    {
        var ticket = Ticket.Raise(reference, "Something is not working", "A description long enough to be realistic.",
            customer.Id, category.Id, TicketPriority.Medium, Now);
        ticket.AssignTo(agent.Id, Now);

        if (status != TicketStatus.New)
        {
            ticket.ChangeStatus(status, Now);
        }

        _db.Add(ticket);
    }
}
