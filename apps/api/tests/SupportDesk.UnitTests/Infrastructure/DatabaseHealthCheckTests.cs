using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SupportDesk.Infrastructure.Data;
using SupportDesk.Presentation.Health;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Infrastructure;

public sealed class DatabaseHealthCheckTests : IDisposable
{
    private readonly SqliteTestDatabase _database = new();

    public void Dispose() => _database.Dispose();

    private static Task<HealthCheckResult> Check(SupportDbContext db, TimeSpan? timeout = null) =>
        new DatabaseHealthCheck(db, timeout).CheckHealthAsync(new HealthCheckContext());

    [Fact]
    public async Task A_database_that_answers_is_healthy()
    {
        var result = await Check(_database.Db);

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task A_database_that_cannot_be_reached_is_unhealthy_without_saying_where_it_was_looked_for()
    {
        await using var unreachable = new SupportDbContext(new DbContextOptionsBuilder<SupportDbContext>()
            .UseSqlite("Data Source=/no/such/folder/support.db;Mode=ReadWrite")
            .Options);

        var result = await Check(unreachable);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.DoesNotContain("/no/such", result.Description);
    }

    [Fact]
    public async Task A_database_that_never_answers_is_unhealthy_instead_of_hanging_the_check()
    {
        var result = await new NeverAnswers(_database.Db, TimeSpan.FromMilliseconds(50))
            .CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Equal("The database did not answer in time.", result.Description);
    }

    [Fact]
    public async Task A_database_that_ignores_the_time_limit_is_still_given_up_on_after_it()
    {
        // The SQL Server driver does exactly this: it keeps connecting for its own timeout and does not stop when asked.
        var started = DateTime.UtcNow;

        var result = await new IgnoresCancellation(_database.Db, TimeSpan.FromMilliseconds(100))
            .CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.True(DateTime.UtcNow - started < TimeSpan.FromSeconds(3), "The check waited far longer than its limit.");
    }

    [Fact]
    public async Task A_database_that_blocks_the_calling_thread_is_still_given_up_on_after_the_limit()
    {
        // Also what the SQL Server driver does when the server is down: the call itself does not return.
        var started = DateTime.UtcNow;

        var result = await new BlocksTheCaller(_database.Db, TimeSpan.FromMilliseconds(100))
            .CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.True(DateTime.UtcNow - started < TimeSpan.FromSeconds(1.5), "The check was held up by the blocked call.");
    }

    [Fact]
    public async Task A_caller_who_gives_up_is_not_told_the_database_is_down()
    {
        using var caller = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
        var check = new NeverAnswers(_database.Db, TimeSpan.FromSeconds(30));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => check.CheckHealthAsync(new HealthCheckContext(), caller.Token));
    }

    [Fact]
    public async Task A_database_that_fails_the_question_is_unhealthy_and_the_failure_is_kept_for_the_log()
    {
        var result = await new Fails(_database.Db).CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.IsType<InvalidOperationException>(result.Exception);
        Assert.DoesNotContain("secret connection details", result.Description);
    }

    /// <summary>Stands in for a database that accepts the connection and then says nothing.</summary>
    private sealed class NeverAnswers(SupportDbContext db, TimeSpan timeout) : DatabaseHealthCheck(db, timeout)
    {
        protected override Task ProbeAsync(CancellationToken cancellationToken) =>
            Task.Delay(Timeout.Infinite, cancellationToken);
    }

    /// <summary>Stands in for a driver that does not stop when it is cancelled.</summary>
    private sealed class IgnoresCancellation(SupportDbContext db, TimeSpan timeout) : DatabaseHealthCheck(db, timeout)
    {
        protected override Task ProbeAsync(CancellationToken cancellationToken) =>
            new TaskCompletionSource().Task;
    }

    /// <summary>Stands in for a connection attempt that blocks before it returns anything.</summary>
    private sealed class BlocksTheCaller(SupportDbContext db, TimeSpan timeout) : DatabaseHealthCheck(db, timeout)
    {
        protected override Task ProbeAsync(CancellationToken cancellationToken)
        {
            Thread.Sleep(TimeSpan.FromSeconds(3));

            return Task.CompletedTask;
        }
    }

    private sealed class Fails(SupportDbContext db) : DatabaseHealthCheck(db)
    {
        protected override Task ProbeAsync(CancellationToken cancellationToken) =>
            throw new InvalidOperationException("secret connection details");
    }

    [Fact]
    public void The_time_limit_is_a_few_seconds_so_a_probe_is_not_left_waiting()
    {
        Assert.InRange(DatabaseHealthCheck.DefaultTimeout, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(10));
    }
}
