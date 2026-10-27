using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.Presentation.Health;

/// <summary>
/// Says whether the API can reach its database right now, by asking it a question and getting an answer.
/// It stops waiting after a few seconds, so a database that hangs makes the check fail instead of making it hang too.
/// </summary>
public class DatabaseHealthCheck(SupportDbContext db, TimeSpan? timeout = null) : IHealthCheck
{
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var limit = timeout ?? DefaultTimeout;

        using var cancelProbe = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cancelProbe.CancelAfter(limit);

        Task? probe = null;

        try
        {
            // On a worker thread, because the SQL Server driver can block the thread that calls it while it
            // tries to connect, before it hands anything back to wait on.
            probe = Task.Run(() => ProbeAsync(cancelProbe.Token), CancellationToken.None);

            // Waiting is bounded here and not left to the probe: the driver keeps trying to connect until
            // its own, longer, timeout whatever it is told, and a health check must not wait that long.
            await probe.WaitAsync(limit, cancellationToken);

            return HealthCheckResult.Healthy("The database answered.");
        }
        catch (Exception exception) when (
            exception is TimeoutException || (exception is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            // The probe may still be running; make sure that whatever it ends up doing is never an unhandled error.
            _ = probe?.ContinueWith(static task => task.Exception, TaskContinuationOptions.OnlyOnFaulted);

            return HealthCheckResult.Unhealthy("The database did not answer in time.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // The exception goes to the log through the health check service; the answer says only that it failed.
            return HealthCheckResult.Unhealthy("The database cannot be reached.", exception);
        }
    }

    /// <summary>
    /// A real query, not EF's CanConnect: that one answers "yes" for an in-memory database without touching
    /// it, and a check that cannot fail is no check. This fails if the connection is gone, the login is
    /// refused or the database does not exist. Separate so a test can stand in a probe that never answers.
    /// </summary>
    protected virtual async Task ProbeAsync(CancellationToken cancellationToken) =>
        await db.Database.ExecuteSqlRawAsync("SELECT 1", cancellationToken);
}
