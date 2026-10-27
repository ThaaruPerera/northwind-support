using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SupportDesk.Application.Contracts.Common;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;
using SupportDesk.Infrastructure.Queries;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Infrastructure.Queries;

/// <summary>
/// Runs the real ticket list query against a real (in-memory SQLite) database, because the
/// point of these tests is that the filtering happens in SQL, before counting and paging.
/// </summary>
public sealed class TicketQueriesFilterTests : IDisposable
{
    private static readonly DateTime Now = FixedClock.DefaultNow;

    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly SupportDbContext _db;
    private readonly TicketQueries _queries;

    private Customer _contoso = null!;
    private Customer _fabrikam = null!;
    private Category _general = null!;
    private Category _billing = null!;
    private Agent _alex = null!;
    private Agent _ben = null!;

    public TicketQueriesFilterTests()
    {
        _connection.Open();

        _db = new SupportDbContext(new DbContextOptionsBuilder<SupportDbContext>()
            .UseSqlite(_connection)
            .Options);
        _db.Database.EnsureCreated();

        Seed();

        _queries = new TicketQueries(_db, new FixedClock(), SlaTestSettings.Default());
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task No_filters_returns_every_ticket()
    {
        var result = await Query(new TicketQuery());

        Assert.Equal(7, result.TotalCount);
    }

    [Fact]
    public async Task Priority_is_an_exact_match_not_a_minimum()
    {
        var result = await Query(new TicketQuery { Priority = TicketPriority.High });

        Assert.Equal(["TCK-0001", "TCK-0002", "TCK-0004"], References(result));
        Assert.Equal(3, result.TotalCount);
    }

    [Fact]
    public async Task Status_is_an_exact_match()
    {
        var result = await Query(new TicketQuery { Status = TicketStatus.New });

        Assert.Equal(["TCK-0002", "TCK-0003", "TCK-0005", "TCK-0006", "TCK-0007"], References(result));
    }

    [Fact]
    public async Task Category_customer_and_agent_are_exact_matches()
    {
        var byCategory = await Query(new TicketQuery { CategoryId = _billing.Id });
        var byCustomer = await Query(new TicketQuery { CustomerId = _contoso.Id });
        var byAgent = await Query(new TicketQuery { AssignedAgentId = _alex.Id });

        Assert.Equal(["TCK-0001", "TCK-0004"], References(byCategory));
        Assert.Equal(["TCK-0002", "TCK-0003", "TCK-0005", "TCK-0006"], References(byCustomer));
        Assert.Equal(["TCK-0003", "TCK-0004"], References(byAgent));
    }

    [Fact]
    public async Task Unassigned_only_returns_tickets_with_no_agent()
    {
        var result = await Query(new TicketQuery { UnassignedOnly = true });

        Assert.Equal(["TCK-0002", "TCK-0005", "TCK-0006", "TCK-0007"], References(result));
    }

    [Fact]
    public async Task Unassigned_only_false_does_not_filter()
    {
        var result = await Query(new TicketQuery { UnassignedOnly = false });

        Assert.Equal(7, result.TotalCount);
    }

    [Fact]
    public async Task Filters_combine_with_and()
    {
        var result = await Query(new TicketQuery
        {
            Priority = TicketPriority.High,
            CustomerId = _fabrikam.Id,
            Status = TicketStatus.Open,
        });

        Assert.Equal(["TCK-0001"], References(result));
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task Filters_that_exclude_each_other_return_nothing()
    {
        var result = await Query(new TicketQuery { AssignedAgentId = _alex.Id, UnassignedOnly = true });

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
        Assert.Equal(0, result.TotalPages);
    }

    [Fact]
    public async Task Search_matches_the_customer_name()
    {
        var result = await Query(new TicketQuery { Search = "fabrikam" });

        Assert.Equal(["TCK-0001", "TCK-0004", "TCK-0007"], References(result));
    }

    [Fact]
    public async Task Search_matches_the_title()
    {
        var result = await Query(new TicketQuery { Search = "invoice" });

        Assert.Equal(["TCK-0001"], References(result));
    }

    [Fact]
    public async Task Search_matches_the_reference()
    {
        var result = await Query(new TicketQuery { Search = "TCK-0003" });

        Assert.Equal(["TCK-0003"], References(result));
    }

    [Fact]
    public async Task Search_is_trimmed()
    {
        var result = await Query(new TicketQuery { Search = "   Fabrikam  " });

        Assert.Equal(3, result.TotalCount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task A_blank_search_is_ignored(string? search)
    {
        var result = await Query(new TicketQuery { Search = search });

        Assert.Equal(7, result.TotalCount);
    }

    [Theory]
    [InlineData("%")]
    [InlineData("_")]
    public async Task Search_treats_wildcard_characters_literally(string search)
    {
        var result = await Query(new TicketQuery { Search = search });

        Assert.Equal(["TCK-0006"], References(result));
    }

    [Fact]
    public async Task Search_and_filters_combine_with_and()
    {
        var result = await Query(new TicketQuery { Search = "contoso", Priority = TicketPriority.High });

        Assert.Equal(["TCK-0002"], References(result));
    }

    [Fact]
    public async Task Total_count_and_pages_describe_the_filtered_set_not_the_table()
    {
        var firstPage = await Query(new TicketQuery { Priority = TicketPriority.High, PageSize = 2 });
        var secondPage = await Query(new TicketQuery { Priority = TicketPriority.High, PageSize = 2, Page = 2 });

        Assert.Equal(3, firstPage.TotalCount);
        Assert.Equal(2, firstPage.TotalPages);
        Assert.Equal(2, firstPage.Items.Count);

        Assert.Equal(3, secondPage.TotalCount);
        Assert.Single(secondPage.Items);
    }

    private Task<PagedResult<TicketListItemDto>> Query(TicketQuery query) =>
        _queries.GetPagedAsync(query.Normalized() with { SortBy = "createdAtUtc", SortDirection = "asc" }, default);

    /// <summary>References in ascending order, so assertions do not depend on the sort order.</summary>
    private static string[] References(PagedResult<TicketListItemDto> result) =>
        result.Items.Select(i => i.Reference).Order().ToArray();

    private void Seed()
    {
        _contoso = new Customer("Contoso Ltd", "help@contoso.test", null, CustomerTier.Premium, Now);
        _fabrikam = new Customer("Fabrikam Inc", "help@fabrikam.test", null, CustomerTier.Standard, Now);
        _general = new Category("General", requiresSpecialist: false, forcesCriticalPriority: false);
        _billing = new Category("Billing", requiresSpecialist: true, forcesCriticalPriority: false);
        _alex = new Agent("Alex Turner", "alex@support.test", 10, Now);
        _ben = new Agent("Ben Osei", "ben@support.test", 8, Now);

        _db.AddRange(_contoso, _fabrikam, _general, _billing, _alex, _ben);
        _db.SaveChanges();

        //  Reference  Customer   Category  Priority  Status  Agent
        Add("TCK-0001", "Invoice total is wrong", _fabrikam, _billing, TicketPriority.High, TicketStatus.Open, _ben);
        Add("TCK-0002", "Cannot sign in", _contoso, _general, TicketPriority.High, TicketStatus.New, null);
        Add("TCK-0003", "Printer jam", _contoso, _general, TicketPriority.Low, TicketStatus.New, _alex);
        Add("TCK-0004", "Refund request", _fabrikam, _billing, TicketPriority.High, TicketStatus.Resolved, _alex);
        Add("TCK-0005", "Region is down", _contoso, _general, TicketPriority.Critical, TicketStatus.New, null);
        Add("TCK-0006", "Discount shows 100% off for snake_case plans", _contoso, _general, TicketPriority.Medium, TicketStatus.New, null);
        Add("TCK-0007", "Question about licences", _fabrikam, _general, TicketPriority.Medium, TicketStatus.New, null);

        _db.SaveChanges();
        _db.ChangeTracker.Clear();
    }

    private void Add(
        string reference,
        string title,
        Customer customer,
        Category category,
        TicketPriority priority,
        TicketStatus status,
        Agent? agent)
    {
        // A distinct creation time per ticket keeps the default sort deterministic.
        var createdAt = Now.AddMinutes(_db.ChangeTracker.Entries<Ticket>().Count());

        var ticket = Ticket.Raise(reference, title, "A description long enough to be realistic.",
            customer.Id, category.Id, priority, createdAt);

        if (agent is not null)
        {
            ticket.AssignTo(agent.Id, createdAt);
        }

        if (status != TicketStatus.New)
        {
            ticket.ChangeStatus(status, createdAt);
        }

        _db.Add(ticket);
    }
}
