using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace SupportDesk.Presentation.Health;

public static class HealthExtensions
{
    public const string Path = "/health";

    public static IServiceCollection AddApiHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database");

        return services;
    }

    /// <summary>
    /// Maps <c>GET /health</c>, which reports whether the database can be reached. It is the one place where
    /// the endpoint is made open to anyone, because whatever checks a running container cannot sign in.
    /// </summary>
    public static IEndpointConventionBuilder MapApiHealth(this IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapHealthChecks(Path, new HealthCheckOptions { ResponseWriter = WriteAsync })
            .AllowAnonymous();

    /// <summary>The status of each check and nothing else: no connection strings, no exception text.</summary>
    private static Task WriteAsync(HttpContext context, HealthReport report) =>
        context.Response.WriteAsJsonAsync(new HealthResponse(
            report.Status.ToString(),
            report.Entries.ToDictionary(entry => entry.Key, entry => entry.Value.Status.ToString())));

    private sealed record HealthResponse(string Status, Dictionary<string, string> Checks);
}
