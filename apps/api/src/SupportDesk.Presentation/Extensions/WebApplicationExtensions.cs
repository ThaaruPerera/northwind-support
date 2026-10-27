using Microsoft.EntityFrameworkCore;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.Presentation.Extensions;

public static class WebApplicationExtensions
{
    /// <summary>
    /// Brings the database schema up to date. A real deployment runs migrations as a separate,
    /// deliberate step instead, so this is only switched on where that is not worth the ceremony.
    /// </summary>
    public static async Task MigrateDatabaseAsync(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();

        await scope.ServiceProvider.GetRequiredService<SupportDbContext>().Database.MigrateAsync();
    }

    /// <summary>
    /// Fills an empty database with the demo support desk, and gives every agent the shared development
    /// password. Demo data and a well known password must never reach a real system, which is why this is
    /// its own switch and not part of migrating.
    /// </summary>
    public static async Task SeedDemoDataAsync(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var seeder = scope.ServiceProvider.GetRequiredService<SupportDbSeeder>();

        await seeder.SeedAsync();
        await seeder.SeedCredentialsAsync();
    }
}
