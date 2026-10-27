using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Infrastructure.Data;

/// <summary>
/// EF Core context for the support desk, and the unit of work that commits each request's
/// changes. Mapping lives in the per-aggregate configuration classes in
/// <c>Data/Configurations</c>.
/// </summary>
public class SupportDbContext(DbContextOptions<SupportDbContext> options) : DbContext(options), IUnitOfWork
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SupportDbContext).Assembly);

        ConfigureSecondsBetween(modelBuilder, SecondsBetweenMethod);
    }

    internal static readonly MethodInfo SecondsBetweenMethod = typeof(SqlFunctions)
        .GetMethod(nameof(SqlFunctions.SecondsBetween), [typeof(DateTime), typeof(DateTime)])!;

    /// <summary>
    /// Says how <see cref="SqlFunctions.SecondsBetween"/> is written for this database. The default
    /// is SQL Server's; a test context for another database overrides it.
    /// </summary>
    protected virtual void ConfigureSecondsBetween(ModelBuilder modelBuilder, MethodInfo method) =>
        modelBuilder.HasDbFunction(method).HasTranslation(arguments => new SqlFunctionExpression(
            "DATEDIFF",
            [new SqlFragmentExpression("second"), arguments[0], arguments[1]],
            nullable: true,
            argumentsPropagateNullability: [false, true, true],
            type: typeof(int),
            typeMapping: null));

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Every timestamp in this database is UTC; say so, so that it survives the round trip.
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        configurationBuilder.Properties<DateTime?>().HaveConversion<NullableUtcDateTimeConverter>();
    }
}
