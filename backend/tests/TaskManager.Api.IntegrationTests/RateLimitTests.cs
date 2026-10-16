using System.Net;
using System.Net.Http.Json;
using TaskManager.Api.IntegrationTests.Infrastructure;
using static TaskManager.Api.IntegrationTests.Infrastructure.ApiTestHelpers;

namespace TaskManager.Api.IntegrationTests;

/// <summary>A factory with the production limit (10 per 60 s per client IP).</summary>
public sealed class RateLimitedApiFactory : TaskManagerApiFactory
{
    public RateLimitedApiFactory()
        : base(new Dictionary<string, string?> { ["RateLimiting:AuthPermitLimit"] = "10" })
    {
    }
}

public sealed class RateLimitTests : IClassFixture<RateLimitedApiFactory>
{
    private readonly RateLimitedApiFactory _factory;

    public RateLimitTests(RateLimitedApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task AC_AUTH_10_EleventhRequestFromOneIp_Is429_WithRetryAfter_OtherIpsUnaffected()
    {
        using var client = _factory.CreateApiClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "203.0.113.10, 10.0.0.1");

        for (var i = 0; i < 10; i++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(client, NewEmail(), "wrongpass1")).StatusCode);
        }

        var limited = await LoginAsync(client, NewEmail(), "wrongpass1");
        await AssertApiErrorAsync(limited, HttpStatusCode.TooManyRequests, "rate_limited");
        Assert.True(int.Parse(limited.Headers.GetValues("Retry-After").Single()) > 0);

        // The register endpoint shares the per-IP budget.
        var register = await client.PostAsJsonAsync("/api/v1/auth/register", new { displayName = "x", email = NewEmail(), password = Password }, ApiJson);
        Assert.Equal(HttpStatusCode.TooManyRequests, register.StatusCode);

        using var otherIp = _factory.CreateApiClient();
        otherIp.DefaultRequestHeaders.Add("X-Forwarded-For", "198.51.100.20");
        Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(otherIp, NewEmail(), "wrongpass1")).StatusCode);

        // Not rate limited: me, tasks.
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/auth/me")).StatusCode);
    }
}
