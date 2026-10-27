using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace SupportDesk.UnitTests.Api;

/// <summary>GET /health over HTTP, against the real application.</summary>
public class HealthEndpointTests(ApiFixture api) : IClassFixture<ApiFixture>
{
    [Fact]
    public async Task Health_is_open_without_a_token_and_reports_the_database_as_healthy()
    {
        using var client = api.Client();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Healthy", body.GetProperty("status").GetString());
        Assert.Equal("Healthy", body.GetProperty("checks").GetProperty("database").GetString());
    }

    [Fact]
    public async Task Health_is_also_open_to_someone_with_a_token_nobody_checks_it()
    {
        var token = await api.SignInAsync(ApiFixture.AlexEmail);
        using var client = api.Client(token);

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Health_says_nothing_about_how_the_database_is_reached()
    {
        using var client = api.Client();

        var text = await (await client.GetAsync("/health")).Content.ReadAsStringAsync();

        Assert.DoesNotContain("Data Source", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sqlite", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Health_answers_503_and_unhealthy_when_the_database_cannot_be_reached()
    {
        var broken = ApiFixture.WithUnreachableDatabase();
        await broken.InitializeAsync();

        try
        {
            using var client = broken.Client();

            var response = await client.GetAsync("/health");

            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("Unhealthy", body.GetProperty("status").GetString());
            Assert.Equal("Unhealthy", body.GetProperty("checks").GetProperty("database").GetString());
            Assert.DoesNotContain("Data Source", body.GetRawText(), StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("/no/such", body.GetRawText());
        }
        finally
        {
            await broken.DisposeAsync();
        }
    }
}
