using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Infrastructure.Data;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Api;

/// <summary>
/// The real application, started in memory the way it starts in Development: its own configuration,
/// its own pipeline, its own controllers. Only the database differs: SQLite in memory instead of SQL Server,
/// filled by the application's own seeder. A request made through this has gone through everything a real one does.
/// </summary>
public sealed class ApiFixture : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly ApiFactory _factory;

    public ApiFixture()
    {
        _connection.Open();
        _factory = new ApiFactory(_connection);
    }

    public IServiceProvider Services => _factory.Services;

    public const string AlexEmail = "alex.turner@northwind-support.example";

    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SupportDbContext>();
        await db.Database.EnsureCreatedAsync();

        var seeder = scope.ServiceProvider.GetRequiredService<SupportDbSeeder>();
        await seeder.SeedAsync();
        await seeder.SeedCredentialsAsync();
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        _connection.Dispose();

        return Task.CompletedTask;
    }

    /// <summary>A client with no token, or with the one given.</summary>
    public HttpClient Client(string? token = null)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        if (token is not null)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return client;
    }

    /// <summary>Signs in through the real login endpoint with the development password and returns the token.</summary>
    public async Task<string> SignInAsync(string email)
    {
        var response = await Client().PostAsJsonAsync("/api/auth/login", new LoginRequest(email, SupportDbSeeder.DevelopmentPassword));
        response.EnsureSuccessStatusCode();

        var login = await response.Content.ReadFromJsonAsync<LoginResponse>();

        return login!.Token;
    }

    /// <summary>Runs something against the database directly, as a test needs to set a scene up.</summary>
    public async Task WithDatabaseAsync(Func<SupportDbContext, Task> action)
    {
        using var scope = _factory.Services.CreateScope();

        await action(scope.ServiceProvider.GetRequiredService<SupportDbContext>());
    }

    private sealed class ApiFactory(SqliteConnection connection) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");

            // The test fills the database itself, with SQLite; the SQL Server migrations are not for it.
            builder.UseSetting("Database:MigrateOnStartup", "false");

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<SupportDbContext>>();
                services.RemoveAll<SupportDbContext>();

                // EF registers a configuration object per context as well, which would still point at SQL Server.
                var leftovers = services
                    .Where(d => d.ServiceType.Name.StartsWith("IDbContextOptionsConfiguration", StringComparison.Ordinal)
                        && d.ServiceType.GenericTypeArguments.Contains(typeof(SupportDbContext)))
                    .ToList();

                foreach (var leftover in leftovers)
                {
                    services.Remove(leftover);
                }

                // The same context the application uses, written for SQLite, handed out in its place.
                services.AddSingleton(new DbContextOptionsBuilder<SupportDbContext>().UseSqlite(connection).Options);
                services.AddScoped<SupportDbContext>(provider =>
                    new SqliteSupportDbContext(provider.GetRequiredService<DbContextOptions<SupportDbContext>>()));
            });
        }
    }
}
