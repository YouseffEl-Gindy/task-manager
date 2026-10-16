using System.Net;
using System.Net.Http.Json;
using TaskManager.Api.Dtos.Auth;
using TaskManager.Api.IntegrationTests.Infrastructure;
using static TaskManager.Api.IntegrationTests.Infrastructure.ApiTestHelpers;

namespace TaskManager.Api.IntegrationTests;

/// <summary>Session cookie, JSON 401s, sliding expiry and security stamp wiring.</summary>
public sealed class AuthSessionTests : IClassFixture<TaskManagerApiFactory>
{
    private readonly TaskManagerApiFactory _factory;

    public AuthSessionTests(TaskManagerApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task AC_AUTH_1_Register_Returns201_UserDto_AndSessionCookieWithRequiredAttributes()
    {
        using var raw = _factory.CreateApiClient(handleCookies: false);
        var email = NewEmail();

        var response = await raw.PostAsJsonAsync(
            "/api/v1/auth/register",
            new { displayName = " Ada Lovelace ", email = "  " + email.ToUpperInvariant() + " ", password = Password },
            ApiJson);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var user = (await response.Content.ReadFromJsonAsync<UserDto>(ApiJson))!;
        Assert.Equal(email, user.Email);
        Assert.Equal("Ada Lovelace", user.DisplayName);

        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"), c => c.StartsWith("tm_session=", StringComparison.Ordinal));
        var attributes = cookie.Split(';').Skip(1).Select(a => a.Trim().ToLowerInvariant()).ToList();
        Assert.Contains("httponly", attributes);
        Assert.Contains("samesite=lax", attributes);
        Assert.Contains("path=/", attributes);
        Assert.Contains("secure", attributes);
        Assert.DoesNotContain(attributes, a => a.StartsWith("expires=") || a.StartsWith("max-age=") || a.StartsWith("domain="));

        using var client = _factory.CreateApiClient();
        await RegisterAsync(client);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/auth/me")).StatusCode);
    }

    [Fact]
    public async Task AC_AUTH_7_ProtectedEndpoint_WithoutOrWithTamperedCookie_Is401ApiError_NeverARedirect()
    {
        using var anonymous = _factory.CreateApiClient();
        await AssertApiErrorAsync(await anonymous.GetAsync("/api/v1/auth/me"), HttpStatusCode.Unauthorized, "unauthorized");
        await AssertApiErrorAsync(await anonymous.GetAsync("/api/v1/tasks"), HttpStatusCode.Unauthorized, "unauthorized");

        using var tampered = _factory.CreateApiClient(handleCookies: false);
        tampered.DefaultRequestHeaders.Add("Cookie", "tm_session=CfDJ8-not-a-real-ticket");
        await AssertApiErrorAsync(await tampered.GetAsync("/api/v1/auth/me"), HttpStatusCode.Unauthorized, "unauthorized");
    }

    [Fact]
    public async Task AC_AUTH_8_Logout_Returns204_ExpiresCookie_EvenWithoutSession()
    {
        using var client = _factory.CreateApiClient();
        await RegisterAsync(client);

        var response = await client.PostAsync("/api/v1/auth/logout", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"), c => c.StartsWith("tm_session=", StringComparison.Ordinal));
        Assert.Contains("expires=Thu, 01 Jan 1970", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/auth/me")).StatusCode);

        using var anonymous = _factory.CreateApiClient();
        Assert.Equal(HttpStatusCode.NoContent, (await anonymous.PostAsync("/api/v1/auth/logout", null)).StatusCode);
    }

    [Fact]
    public async Task AC_AUTH_9_Session_ExpiresAfterInactivity_ButSlidesWhenUsed()
    {
        // The factory's FakeTimeProvider is shared by the class; each step only moves it forward.
        using var idle = _factory.CreateApiClient();
        await RegisterAsync(idle);
        using var active = _factory.CreateApiClient();
        await RegisterAsync(active);

        for (var i = 0; i < 3; i++)
        {
            _factory.Time.Advance(TimeSpan.FromHours(4) + TimeSpan.FromMinutes(1));
            Assert.Equal(HttpStatusCode.OK, (await active.GetAsync("/api/v1/auth/me")).StatusCode);
        }

        // 12+ hours total: the active session is still valid; the idle one expired after 8 hours.
        Assert.Equal(HttpStatusCode.Unauthorized, (await idle.GetAsync("/api/v1/auth/me")).StatusCode);
    }

    [Fact]
    public async Task AC_PWD_4_ChangePassword_KeepsThisSession_AndEndsOtherSessions()
    {
        using var sessionA = _factory.CreateApiClient();
        var email = NewEmail();
        await RegisterAsync(sessionA, email);
        using var sessionB = _factory.CreateApiClient();
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(sessionB, email)).StatusCode);

        var change = await sessionA.PostAsJsonAsync(
            "/api/v1/account/change-password",
            new { currentPassword = Password, newPassword = "batterystaple2" },
            ApiJson);

        Assert.Equal(HttpStatusCode.NoContent, change.StatusCode);

        // The stamp is re-checked only when (now - issued) > interval, strictly. With a frozen FakeTimeProvider
        // and an interval of 0 that is never true, so move time past the interval.
        _factory.Time.Advance(TimeSpan.FromSeconds(1));

        Assert.Equal(HttpStatusCode.OK, (await sessionA.GetAsync("/api/v1/auth/me")).StatusCode);
        await AssertApiErrorAsync(await sessionB.GetAsync("/api/v1/auth/me"), HttpStatusCode.Unauthorized, "unauthorized");
    }

    [Fact]
    public async Task AC_AUTH_5_WrongPasswordAndUnknownEmail_SameInvalidCredentialsBody()
    {
        using var client = _factory.CreateApiClient();
        var email = NewEmail();
        await RegisterAsync(client, email);

        var wrong = await AssertApiErrorAsync(await LoginAsync(client, email, "wrongpass1"), HttpStatusCode.Unauthorized, "invalid_credentials");
        var unknown = await AssertApiErrorAsync(await LoginAsync(client, NewEmail()), HttpStatusCode.Unauthorized, "invalid_credentials");

        Assert.Equal(wrong.GetProperty("message").GetString(), unknown.GetProperty("message").GetString());
    }
}
