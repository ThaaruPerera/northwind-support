using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using SupportDesk.Application.Authentication;
using SupportDesk.Infrastructure.Services;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Api;

/// <summary>
/// Proves, against the running application and not by reading its code, that every endpoint it exposes
/// refuses a request without a valid token, apart from the ones that are open on purpose.
/// </summary>
public class EndpointProtectionTests(ApiFixture api) : IClassFixture<ApiFixture>
{
    /// <summary>
    /// The only endpoints anyone may call without signing in. Swagger is middleware, not an endpoint, and is
    /// checked on its own below. "ANY" is an endpoint that does not name a method, as the health check does.
    /// </summary>
    private static readonly string[] OpenOnPurpose = ["POST api/auth/login", "ANY health"];

    private sealed record Route(string Method, string Pattern, bool IsOpen)
    {
        public string Key => $"{Method} {Pattern}";

        /// <summary>A real address for the pattern: every parameter becomes 1, which exists in the seed data.</summary>
        public string Url => "/" + Regex.Replace(Pattern, @"\{[^}]+\}", "1");
    }

    /// <summary>Every route the running application exposes, found by asking it, not by listing them here.</summary>
    private List<Route> AllRoutes() => api.Services
        .GetServices<EndpointDataSource>()
        .SelectMany(source => source.Endpoints)
        .OfType<RouteEndpoint>()
        .SelectMany(endpoint =>
        {
            var methods = endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods ?? [];
            var isOpen = endpoint.Metadata.GetMetadata<Microsoft.AspNetCore.Authorization.IAllowAnonymous>() is not null;
            var pattern = (endpoint.RoutePattern.RawText ?? string.Empty).TrimStart('/');

            return (methods.Count > 0 ? methods : ["ANY"]).Select(method => new Route(method, pattern, isOpen));
        })
        .ToList();

    private static HttpRequestMessage Request(Route route) =>
        new(route.Method == "ANY" ? HttpMethod.Get : new HttpMethod(route.Method), route.Url);

    [Fact]
    public void This_test_is_looking_at_the_real_endpoints_and_not_at_an_empty_list()
    {
        var keys = AllRoutes().Select(route => route.Key).ToList();

        Assert.Contains("GET api/tickets", keys);
        Assert.Contains("GET api/tickets/{id:int}", keys);
        Assert.Contains("POST api/tickets", keys);
        Assert.Contains("PATCH api/tickets/{id:int}/status", keys);
        Assert.Contains("PATCH api/tickets/{id:int}/assignment", keys);
        Assert.Contains("POST api/tickets/{id:int}/escalate", keys);
        Assert.Contains("GET api/tickets/{id:int}/escalations", keys);
        Assert.Contains("GET api/customers", keys);
        Assert.Contains("GET api/agents", keys);
        Assert.Contains("GET api/categories", keys);
        Assert.Contains("GET api/auth/me", keys);
        Assert.Contains("POST api/auth/login", keys);
        Assert.Contains("ANY health", keys);
        Assert.True(keys.Count >= 13, $"Only {keys.Count} endpoints were found.");
    }

    [Fact]
    public void Only_the_endpoints_that_are_open_on_purpose_are_open()
    {
        var open = AllRoutes().Where(route => route.IsOpen).Select(route => route.Key).Order().ToList();

        Assert.Equal(OpenOnPurpose.Order(), open);
    }

    [Fact]
    public async Task Every_other_endpoint_answers_401_to_a_request_with_no_token()
    {
        using var client = api.Client();
        var offenders = new List<string>();

        foreach (var route in AllRoutes().Where(route => !OpenOnPurpose.Contains(route.Key)))
        {
            var response = await client.SendAsync(Request(route));

            if (response.StatusCode != HttpStatusCode.Unauthorized)
            {
                offenders.Add($"{route.Key} answered {(int)response.StatusCode}");
            }
        }

        Assert.True(offenders.Count == 0, "These endpoints did not ask for a token: " + string.Join("; ", offenders));
    }

    [Fact]
    public async Task Every_other_endpoint_lets_a_signed_in_agent_through_to_the_endpoint_itself()
    {
        var token = await api.SignInAsync(ApiFixture.AlexEmail);
        using var client = api.Client(token);
        var turnedAway = new List<string>();

        foreach (var route in AllRoutes().Where(route => !OpenOnPurpose.Contains(route.Key)))
        {
            var response = await client.SendAsync(Request(route));

            // No body is sent, so some answer 400 or 415. What matters is that the token was accepted.
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                turnedAway.Add($"{route.Key} answered {(int)response.StatusCode}");
            }
        }

        Assert.True(turnedAway.Count == 0, "These endpoints turned away a valid token: " + string.Join("; ", turnedAway));
    }

    [Fact]
    public async Task A_request_with_no_token_gets_a_problem_document_that_says_to_sign_in()
    {
        using var client = api.Client();

        var response = await client.GetAsync("/api/tickets");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("Bearer", response.Headers.WwwAuthenticate.ToString());
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Unauthorized", problem.GetProperty("title").GetString());
        Assert.Equal(401, problem.GetProperty("status").GetInt32());
        Assert.Equal("Sign in to continue.", problem.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Swagger_stays_open_without_a_token_and_offers_the_authorize_button()
    {
        using var client = api.Client();

        var response = await client.GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var document = await response.Content.ReadAsStringAsync();
        Assert.Contains("securitySchemes", document);
        Assert.Contains("\"bearer\"", document);
    }

    [Fact]
    public async Task A_valid_token_is_accepted_on_a_real_endpoint()
    {
        var token = await api.SignInAsync(ApiFixture.AlexEmail);
        using var client = api.Client(token);

        var response = await client.GetAsync("/api/tickets");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    public static IEnumerable<object[]> BadTokens()
    {
        yield return ["garbage"];
        yield return ["tampered"];
        yield return ["expired"];
        yield return ["signed with another key"];
        yield return ["for another audience"];
        yield return ["from another issuer"];
    }

    [Theory]
    [MemberData(nameof(BadTokens))]
    public async Task A_token_that_is_not_ours_is_refused(string kind)
    {
        var real = api.Services.GetRequiredService<JwtSettings>();
        var token = kind switch
        {
            "garbage" => "not.a.token",
            "tampered" => (await api.SignInAsync(ApiFixture.AlexEmail))[..^3] + "abc",
            "expired" => Issue(real, new FixedClock()),
            "signed with another key" => Issue(With(real, key: new string('x', 40)), new SystemClock()),
            "for another audience" => Issue(With(real, audience: "someone.else"), new SystemClock()),
            _ => Issue(With(real, issuer: "someone.else"), new SystemClock())
        };
        using var client = api.Client(token);

        var response = await client.GetAsync("/api/tickets");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task The_same_kind_of_token_is_accepted_when_it_is_made_properly()
    {
        // The control for the test above: a token built the same way, but with our key and in date, works.
        var real = api.Services.GetRequiredService<JwtSettings>();
        using var client = api.Client(Issue(real, new SystemClock()));

        var response = await client.GetAsync("/api/tickets");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static string Issue(JwtSettings settings, SupportDesk.Application.Abstractions.IClock clock) =>
        new JwtAccessTokenIssuer(settings, clock).Issue(1, "Alex Turner", ApiFixture.AlexEmail).Token;

    private static JwtSettings With(JwtSettings real, string? key = null, string? audience = null, string? issuer = null) => new()
    {
        SigningKey = key ?? real.SigningKey,
        Audience = audience ?? real.Audience,
        Issuer = issuer ?? real.Issuer,
        LifetimeMinutes = real.LifetimeMinutes
    };
}
