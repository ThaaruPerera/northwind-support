namespace SupportDesk.Infrastructure.Data;

/// <summary>
/// Functions that exist only inside a database query. Calling one in ordinary code is a mistake,
/// so it throws; <see cref="SupportDbContext"/> says how each is written in SQL.
/// </summary>
public static class SqlFunctions
{
    /// <summary>
    /// Whole-second boundaries crossed going from <paramref name="start"/> to <paramref name="end"/>,
    /// which is what SQL Server's <c>DATEDIFF(second, ...)</c> counts. Seconds, not milliseconds, so a
    /// ticket that has been overdue for months cannot overflow the result.
    /// </summary>
    public static int SecondsBetween(DateTime start, DateTime end) =>
        throw new NotSupportedException($"{nameof(SecondsBetween)} can only be used in a database query.");
}
