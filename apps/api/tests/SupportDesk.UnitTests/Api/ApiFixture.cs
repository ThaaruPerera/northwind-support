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
/// The real application, started in memory: its own configuration, its own pipeline, its own controllers.
/// Only the database differs: SQLite in memory instead of SQL Server. By default it starts the way it does in
/// Development and is filled by the application's own seeder. The other starts behave like a deployment,
/// without the development settings file. A request made through this has gone through everything a real one does.
/// </summary>
public sealed class ApiFixture : IAsyncLifetime
{
    /// <summary>The signing key a production start is given, standing in for a secret from the environment.</summary>
    public const string ProductionSigningKey = "a-production-test-key-of-at-least-32-bytes-long";

    public const string AlexEmail = "alex.turner@northwind-support.example";

    private sealed record Start(string Environment, bool SeedDemoDataAtStartup, bool DatabaseIsUnreachable);

    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly Start _start;
    private readonly ApiFactory _factory;

    public ApiFixture() : this(new Start("Development", SeedDemoDataAtStartup: false, DatabaseIsUnreachable: false))
    {
    }

    private ApiFixture(Start start)
    {
        _start = start;
        _connection.Open();
        _factory = new ApiFactory(_connection, start);
    }

    /// <summary>The same application, but its database is somewhere that cannot be opened, as if the server were gone.</summary>
    public static ApiFixture WithUnreachableDatabase() =>
        new(new Start("Development", SeedDemoDataAtStartup: false, DatabaseIsUnreachable: true));

    /// <summary>Started as a deployment is: Production, no development settings, and nothing asked to be seeded.</summary>
    public static ApiFixture ProductionWithoutDemoData() =>
        new(new Start("Production", SeedDemoDataAtStartup: false, DatabaseIsUnreachable: false));

    /// <summary>Started like the demo stack: Production settings, with seeding switched on.</summary>
    public static ApiFixture ProductionSeedingDemoData() =>
        new(new Start("Production", SeedDemoDataAtStartup: true, DatabaseIsUnreachable: false));

    public IServiceProvider Services => _factory.Services;

    public async Task InitializeAsync()
    {
        if (_start.DatabaseIsUnreachable)
        {
            return;
        }

        // The schema comes first, because a start that seeds needs it to be there when the application begins.
        await using (var schema = new SqliteSupportDbContext(
            new DbContextOptionsBuilder<SupportDbContext>().UseSqlite(_connection).Options))
        {
            await schema.Database.EnsureCreatedAsync();
        }

        if (_start.Environment != "Development" || _start.SeedDemoDataAtStartup)
        {
            // Starting the host is what seeds, when it has been asked to; or, for the empty start, nothing happens.
            _ = _factory.Services;

            return;
        }

        using var scope = _factory.Services.CreateScope();
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

    private sealed class ApiFactory(SqliteConnection connection, Start start) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(start.Environment);

            if (start.Environment == "Development")
            {
                // Development would migrate and seed at startup. The test fills the database itself, with SQLite;
                // the SQL Server migrations are not for it.
                builder.UseSetting("Database:MigrateOnStartup", "false");
                builder.UseSetting("Database:SeedDemoData", "false");
            }
            else
            {
                // What a container is given: values from the environment, since there is no development file.
                // Migrating and seeding are left at their defaults, which is off, unless seeding is asked for.
                builder.UseSetting("Jwt:SigningKey", ProductionSigningKey);
                builder.UseSetting("ConnectionStrings:Default", "Server=unused");

                if (start.SeedDemoDataAtStartup)
                {
                    builder.UseSetting("Database:SeedDemoData", "true");
                }
            }

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

                var options = new DbContextOptionsBuilder<SupportDbContext>();

                if (start.DatabaseIsUnreachable)
                {
                    options.UseSqlite("Data Source=/no/such/folder/support.db;Mode=ReadWrite");
                }
                else
                {
                    options.UseSqlite(connection);
                }

                // The same context the application uses, written for SQLite, handed out in its place.
                services.AddSingleton(options.Options);
                services.AddScoped<SupportDbContext>(provider =>
                    new SqliteSupportDbContext(provider.GetRequiredService<DbContextOptions<SupportDbContext>>()));
            });
        }
    }
}
