using System.Net;
using Microsoft.AspNetCore.Http;
using TaskManager.Api.RateLimiting;

namespace TaskManager.Api.UnitTests.RateLimiting;

/// <summary>Rate-limit partition key (SPEC 5.3).</summary>
public sealed class ClientIpResolverTests
{
    private static HttpContext Context(string? forwardedFor, string remoteIp = "10.0.0.9")
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(remoteIp);
        if (forwardedFor is not null)
        {
            context.Request.Headers[ClientIpResolver.ForwardedForHeader] = forwardedFor;
        }

        return context;
    }

    [Fact]
    public void UseForwardedFor_TakesTheLeftMostValue()
    {
        Assert.Equal("203.0.113.7", ClientIpResolver.Resolve(Context("203.0.113.7, 10.1.1.1, 10.2.2.2"), useForwardedFor: true));
    }

    [Fact]
    public void UseForwardedFor_WithoutHeader_FallsBackToConnectionIp()
    {
        Assert.Equal("10.0.0.9", ClientIpResolver.Resolve(Context(null), useForwardedFor: true));
        Assert.Equal("10.0.0.9", ClientIpResolver.Resolve(Context("  "), useForwardedFor: true));
    }

    [Fact]
    public void ForwardedForDisabled_IgnoresTheHeader()
    {
        Assert.Equal("10.0.0.9", ClientIpResolver.Resolve(Context("203.0.113.7"), useForwardedFor: false));
    }

    [Fact]
    public void NoConnectionIp_UsesUnknown()
    {
        Assert.Equal(ClientIpResolver.Unknown, ClientIpResolver.Resolve(new DefaultHttpContext(), useForwardedFor: false));
    }
}
