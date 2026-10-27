using Microsoft.EntityFrameworkCore;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.Presentation.Extensions;

public static class WebApplicationExtensions
{
    /// <summary>
    /// Applies pending migrations and seeds the demo data. Development convenience only -
    /// a real deployment would run migrations as a separate, deliberate step.
    /// </summary>
    public static async Task MigrateAndSeedAsync(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();

        await scope.ServiceProvider.GetRequiredService<SupportDbContext>().Database.MigrateAsync();
        var seeder = scope.ServiceProvider.GetRequiredService<SupportDbSeeder>();

        await seeder.SeedAsync();
        await seeder.SeedCredentialsAsync();
    }
}
