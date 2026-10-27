using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;
using SupportDesk.Infrastructure.Queries;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Infrastructure.Queries;

/// <summary>
/// Filtering by SLA status happens in SQL, while the status shown on a ticket is worked out in C#.
/// These tests run the real query against a real database and require the two to agree on every
/// ticket, including the ones sitting exactly on a boundary.
/// </summary>
public sealed class SlaStatusFilterTests : IDisposable
{
    private static readonly DateTime Now = FixedClock.DefaultNow;
    private static readonly TimeSpan Window = TimeSpan.FromHours(8);
    private static readonly SlaSettings Settings = SlaTestSettings.Default();

    private readonly SqliteTestDatabase _database = new();
    private readonly TicketQueries _queries;
    private readonly Dictionary<string, Ticket> _tickets = [];

    public SlaStatusFilterTests()
    {
        var customer = new Customer("Fabrikam Inc", "a@fabrikam.test", null, CustomerTier.Standard, Now);
        var category = new Category("General", false, false);
        Db.AddRange(customer, category);
        Db.SaveChanges();

        // Each ticket's window is eight hours, so 25% is the last two.
        Add("no due date", customer, category, due: null);
        Add("plenty of time", customer, category, due: Now.AddHours(7));
        Add("just over the threshold", customer, category, due: Now.AddHours(2).AddSeconds(1));
        Add("exactly at the threshold", customer, category, due: Now.AddHours(2));
        Add("just under the threshold", customer, category, due: Now.AddHours(2).AddSeconds(-1));
        Add("due this very second", customer, category, due: Now);
        Add("one second late", customer, category, due: Now.AddSeconds(-1));
        Add("overdue for two months", customer, category, due: Now.AddDays(-60));
        Add("resolved well before it was due", customer, category, due: Now.AddDays(-1), resolvedAfter: Window - TimeSpan.FromHours(1));
        Add("resolved exactly when due", customer, category, due: Now.AddDays(-1), resolvedAfter: Window);
        Add("resolved one second late", customer, category, due: Now.AddDays(-1), resolvedAfter: Window + TimeSpan.FromSeconds(1));
        Add("resolved early with the due date ahead", customer, category, due: Now.AddHours(5), resolvedAfter: TimeSpan.FromHours(1));
        Add("closed on time", customer, category, due: Now.AddDays(-1), resolvedAfter: TimeSpan.FromHours(2), close: true);
        Add("a window of no length, in the future", customer, category, due: Now.AddHours(1), windowStartsAtDue: true);

        _queries = new TicketQueries(Db, new FixedClock(), Settings);
    }

    private SupportDbContext Db => _database.Db;

    public void Dispose() => _database.Dispose();

    [Theory]
    [InlineData(SlaStatus.NotApplicable)]
    [InlineData(SlaStatus.WithinSla)]
    [InlineData(SlaStatus.AtRisk)]
    [InlineData(SlaStatus.Breached)]
    [InlineData(SlaStatus.Met)]
    public async Task The_sql_filter_returns_exactly_the_tickets_the_evaluator_calls_that_status(SlaStatus status)
    {
        var expected = _tickets
            .Where(pair => Evaluate(pair.Value) == status)
            .Select(pair => pair.Key)
            .Order()
            .ToArray();

        var result = await _queries.GetPagedAsync(new TicketQuery { SlaStatus = status, PageSize = 100 }, default);
        var actual = result.Items.Select(item => TitleOf(item.Id)).Order().ToArray();

        Assert.NotEmpty(expected);
        Assert.Equal(expected, actual);
        Assert.Equal(expected.Length, result.TotalCount);
    }

    [Fact]
    public async Task Every_ticket_is_in_exactly_one_status()
    {
        var found = new List<string>();

        foreach (var status in Enum.GetValues<SlaStatus>())
        {
            var result = await _queries.GetPagedAsync(new TicketQuery { SlaStatus = status, PageSize = 100 }, default);
            found.AddRange(result.Items.Select(item => TitleOf(item.Id)));
        }

        Assert.Equal(_tickets.Keys.Order(), found.Order());
    }

    [Fact]
    public async Task A_ticket_one_second_past_its_due_date_is_breached_and_one_exactly_on_it_is_not()
    {
        var breached = await Titles(SlaStatus.Breached);

        Assert.Contains("one second late", breached);
        Assert.DoesNotContain("due this very second", breached);
    }

    [Fact]
    public async Task A_ticket_overdue_for_months_does_not_break_the_query()
    {
        Assert.Contains("overdue for two months", await Titles(SlaStatus.Breached));
    }

    [Fact]
    public async Task The_count_and_pages_are_for_the_filtered_set_and_paging_happens_after_filtering()
    {
        var breachedCount = _tickets.Values.Count(t => Evaluate(t) == SlaStatus.Breached);
        Assert.True(breachedCount >= 3);

        var first = await _queries.GetPagedAsync(new TicketQuery { SlaStatus = SlaStatus.Breached, PageSize = 2 }, default);
        var second = await _queries.GetPagedAsync(new TicketQuery { SlaStatus = SlaStatus.Breached, PageSize = 2, Page = 2 }, default);

        Assert.Equal(breachedCount, first.TotalCount);
        Assert.Equal((int)Math.Ceiling(breachedCount / 2.0), first.TotalPages);
        Assert.Equal(2, first.Items.Count);
        Assert.Empty(first.Items.Select(i => i.Id).Intersect(second.Items.Select(i => i.Id)));
    }

    [Fact]
    public async Task The_status_filter_combines_with_other_filters_using_and()
    {
        var result = await _queries.GetPagedAsync(
            new TicketQuery { SlaStatus = SlaStatus.Breached, Priority = TicketPriority.Critical, PageSize = 100 }, default);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
    }

    [Fact]
    public async Task No_status_filter_returns_every_ticket()
    {
        var result = await _queries.GetPagedAsync(new TicketQuery { PageSize = 100 }, default);

        Assert.Equal(_tickets.Count, result.TotalCount);
    }

    private async Task<string[]> Titles(SlaStatus status)
    {
        var result = await _queries.GetPagedAsync(new TicketQuery { SlaStatus = status, PageSize = 100 }, default);

        return result.Items.Select(item => TitleOf(item.Id)).ToArray();
    }

    private string TitleOf(int id) => _tickets.Single(pair => pair.Value.Id == id).Key;

    private static SlaStatus Evaluate(Ticket t) => SlaEvaluator.Evaluate(
        t.DueAtUtc, t.ResolvedAtUtc, Now, t.SlaStartedAtUtc, Settings.AtRiskThresholdPercent);

    private void Add(
        string name,
        Customer customer,
        Category category,
        DateTime? due,
        TimeSpan? resolvedAfter = null,
        bool close = false,
        bool windowStartsAtDue = false)
    {
        var started = due is null ? Now : windowStartsAtDue ? due.Value : due.Value - Window;

        var ticket = Ticket.Raise(
            $"TCK-{_tickets.Count + 1:D4}", name, "A description long enough to be realistic.",
            customer.Id, category.Id, TicketPriority.Medium, started);

        if (due is not null)
        {
            ticket.ApplyTriage(due.Value, assignedAgentId: null, "Nobody is eligible.", started);
        }

        if (resolvedAfter is not null)
        {
            ticket.ChangeStatus(close ? TicketStatus.Closed : TicketStatus.Resolved, started + resolvedAfter.Value);
        }

        Db.Add(ticket);
        Db.SaveChanges();
        _tickets[name] = ticket;
    }
}
