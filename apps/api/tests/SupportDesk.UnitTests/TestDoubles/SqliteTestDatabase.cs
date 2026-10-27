using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.UnitTests.TestDoubles;

/// <summary>
/// A real, empty, in-memory SQLite database with the application's schema, for tests of what the
/// queries and repositories actually ask the database. Each instance is its own database.
/// </summary>
public sealed class SqliteTestDatabase : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public SqliteTestDatabase()
    {
        _connection.Open();

        Db = new SupportDbContext(new DbContextOptionsBuilder<SupportDbContext>().UseSqlite(_connection).Options);
        Db.Database.EnsureCreated();
    }

    public SupportDbContext Db { get; }

    public void Dispose()
    {
        Db.Dispose();
        _connection.Dispose();
    }
}
