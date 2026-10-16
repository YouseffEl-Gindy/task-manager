using TaskManager.Api;
using TaskManager.Api.Auth;
using TaskManager.Api.Data;
using TaskManager.Api.Middleware;
using TaskManager.Api.OpenApi;
using TaskManager.Api.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// No CORS policy on purpose: the browser only talks to the Next.js origin, and the CSRF header check
// relies on cross-site preflights failing (SPEC 4.1 FR-A6, 5.3).
builder.Services.AddAppDatabase(builder.Configuration);   // fails at startup if Database:Provider/Schema are invalid
builder.Services.AddAppIdentity(builder.Configuration);
builder.Services.AddAppRateLimiting(builder.Configuration);
builder.Services.AddAppApi();
builder.Services.AddAppOpenApi();

var app = builder.Build();

await app.Services.ApplyDatabaseSchemaAsync();

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseApiStatusCodeErrors();
app.UseMiddleware<CsrfHeaderMiddleware>();   // before authentication and model binding
app.UseRouting();
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

app.UseAppOpenApi();   // dev only: /openapi/v1.json + /swagger
app.MapHealthChecks("/health");
app.MapControllers();

await app.RunAsync();

/// <summary>Entry point; public so integration tests can use <c>WebApplicationFactory&lt;Program&gt;</c>.</summary>
public partial class Program;
