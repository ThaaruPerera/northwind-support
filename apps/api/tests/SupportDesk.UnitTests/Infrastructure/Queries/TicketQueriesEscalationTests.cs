using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;
using SupportDesk.Infrastructure.Queries;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Infrastructure.Queries;

/// <summary>Reads a ticket's escalation history back through the real query, on a real database.</summary>
public sealed class TicketQueriesEscalationTests : IDisposable
{
    private static readonly DateTime Now = FixedClock.DefaultNow;

    private readonly SqliteTestDatabase _database = new();
    private readonly TicketQueries _queries;

    private readonly Ticket _ticket;
    private readonly Ticket _otherTicket;
    private readonly Agent _alex;
    private readonly Agent _ben;

    public TicketQueriesEscalationTests()
    {
        var customer = new Customer("Fabrikam Inc", "a@fabrikam.test", null, CustomerTier.Standard, Now);
        var category = new Category("General", false, false);
        _alex = new Agent("Alex Turner", "alex@support.test", 10, Now);
        _ben = new Agent("Ben Osei", "ben@support.test", 8, Now);

        Db.AddRange(customer, category, _alex, _ben);
        Db.SaveChanges();

        _ticket = Ticket.Raise("TCK-0001", "First ticket", "A description long enough to be realistic.", customer.Id, category.Id, TicketPriority.Low, Now);
        _otherTicket = Ticket.Raise("TCK-0002", "Second ticket", "A description long enough to be realistic.", customer.Id, category.Id, TicketPriority.Low, Now);
        Db.AddRange(_ticket, _otherTicket);
        Db.SaveChanges();

        _queries = new TicketQueries(Db, new FixedClock(), SlaTestSettings.Default());
    }

    private SupportDbContext Db => _database.Db;

    public void Dispose() => _database.Dispose();

    private void Escalate(Ticket ticket, TicketPriority from, TicketPriority to, int? fromAgent, int? toAgent, int hoursAfter, string reason = "Customer is waiting")
    {
        Db.Add(new TicketEscalation(
            ticket.Id, from, to, fromAgent, toAgent, fromDueAtUtc: null, Now.AddHours(hoursAfter + 4), reason, "Dana", Now.AddHours(hoursAfter)));
        Db.SaveChanges();
        Db.ChangeTracker.Clear();
    }

    [Fact]
    public async Task The_history_comes_back_newest_first()
    {
        Escalate(_ticket, TicketPriority.Low, TicketPriority.Medium, null, _alex.Id, hoursAfter: 1, "first");
        Escalate(_ticket, TicketPriority.Medium, TicketPriority.High, _alex.Id, _alex.Id, hoursAfter: 5, "third");
        Escalate(_ticket, TicketPriority.Medium, TicketPriority.High, _alex.Id, _ben.Id, hoursAfter: 3, "second");

        var history = await _queries.GetEscalationsAsync(_ticket.Id, default);

        Assert.Equal(["third", "second", "first"], history.Select(e => e.Reason));
    }

    [Fact]
    public async Task Each_row_carries_the_before_and_after_with_agent_names()
    {
        Escalate(_ticket, TicketPriority.Medium, TicketPriority.High, _alex.Id, _ben.Id, hoursAfter: 2);

        var row = Assert.Single(await _queries.GetEscalationsAsync(_ticket.Id, default));

        Assert.Equal(TicketPriority.Medium, row.FromPriority);
        Assert.Equal(TicketPriority.High, row.ToPriority);
        Assert.Equal("Alex Turner", row.FromAgent?.FullName);
        Assert.Equal("Ben Osei", row.ToAgent?.FullName);
        Assert.Equal("Dana", row.EscalatedBy);
        Assert.Equal(Now.AddHours(2), row.EscalatedAtUtc);
        Assert.Equal(Now.AddHours(6), row.ToDueAtUtc);
        Assert.Null(row.FromDueAtUtc);
    }

    [Fact]
    public async Task A_ticket_that_had_no_owner_or_loses_its_owner_has_no_agent_on_that_side()
    {
        Escalate(_ticket, TicketPriority.Low, TicketPriority.Medium, null, null, hoursAfter: 1);

        var row = Assert.Single(await _queries.GetEscalationsAsync(_ticket.Id, default));

        Assert.Null(row.FromAgent);
        Assert.Null(row.ToAgent);
    }

    [Fact]
    public async Task Only_the_requested_tickets_history_is_returned()
    {
        Escalate(_ticket, TicketPriority.Low, TicketPriority.Medium, null, null, hoursAfter: 1, "mine");
        Escalate(_otherTicket, TicketPriority.Low, TicketPriority.Medium, null, null, hoursAfter: 1, "not mine");

        var history = await _queries.GetEscalationsAsync(_ticket.Id, default);

        Assert.Equal(["mine"], history.Select(e => e.Reason));
    }

    [Fact]
    public async Task A_ticket_that_was_never_escalated_has_an_empty_history() =>
        Assert.Empty(await _queries.GetEscalationsAsync(_ticket.Id, default));

    [Fact]
    public async Task A_ticket_is_known_to_exist_or_not()
    {
        Assert.True(await _queries.ExistsAsync(_ticket.Id, default));
        Assert.False(await _queries.ExistsAsync(9999, default));
    }
}
