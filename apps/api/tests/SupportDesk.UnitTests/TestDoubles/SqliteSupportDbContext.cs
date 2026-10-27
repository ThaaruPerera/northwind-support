using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.UnitTests.TestDoubles;

/// <summary>
/// The application's context, with its SQL Server only function written the SQLite way, so a query
/// that uses it can run in a test. SQL Server counts the second boundaries crossed between two
/// times, which is the difference of the two times truncated to whole seconds: exactly what
/// subtracting <c>unixepoch</c> values gives.
/// </summary>
internal sealed class SqliteSupportDbContext(DbContextOptions<SupportDbContext> options) : SupportDbContext(options)
{
    protected override void ConfigureSecondsBetween(ModelBuilder modelBuilder, MethodInfo method) =>
        modelBuilder.HasDbFunction(method).HasTranslation(arguments => new SqlBinaryExpression(
            ExpressionType.Subtract,
            UnixSeconds(arguments[1]),
            UnixSeconds(arguments[0]),
            typeof(int),
            typeMapping: null)!);

    private static SqlExpression UnixSeconds(SqlExpression time) => new SqlFunctionExpression(
        "unixepoch",
        [time],
        nullable: true,
        argumentsPropagateNullability: [true],
        type: typeof(int),
        typeMapping: null);
}
