using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.OpenApi;
using TaskManager.Api.Auth;
using TaskManager.Api.Json;

namespace TaskManager.Api.OpenApi;

/// <summary>
/// OpenAPI document at <c>/openapi/v1.json</c> and Swagger UI at <c>/swagger</c>, both only when
/// <c>OpenApi:Enabled</c> is true (set in <c>appsettings.Development.json</c>, never in production).
/// </summary>
public static class OpenApiServiceExtensions
{
    public const string EnabledKey = "OpenApi:Enabled";
    public const string DocumentPath = "/openapi/v1.json";

    public static IServiceCollection AddAppOpenApi(this IServiceCollection services)
    {
        // The OpenAPI generator reads the minimal-API JSON options, not MVC's, so give it the same settings;
        // otherwise enums are documented as integers. No minimal-API endpoint returns JSON, so nothing else changes.
        services.ConfigureHttpJsonOptions(options => ApiJsonOptions.Apply(options.SerializerOptions));

        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer((document, _, _) =>
            {
                document.Info.Title = "Task Manager API";
                document.Info.Description =
                    "Log in with POST /api/v1/auth/login (or register) first: the browser keeps the tm_session cookie. " +
                    $"Swagger UI adds the required {CsrfHeaderMiddleware.HeaderName}: {CsrfHeaderMiddleware.HeaderValue} header to every request. " +
                    $"Without it, POST/PUT/DELETE return 403 csrf_failed.";
                return Task.CompletedTask;
            });

            // Positional records make every constructor parameter "required" in the schema. For request DTOs, only
            // [JsonRequired] or non-nullable properties really are (API.md section 6), e.g. CreateTaskRequest.status is optional.
            options.AddSchemaTransformer((schema, context, _) =>
            {
                var typeInfo = context.JsonTypeInfo;
                if (context.JsonPropertyInfo is null
                    && typeInfo.Kind == JsonTypeInfoKind.Object
                    && typeInfo.Type.Name.EndsWith("Request", StringComparison.Ordinal))
                {
                    schema.Required = typeInfo.Properties
                        .Where(property => property.IsRequired || !property.IsSetNullable)
                        .Select(property => property.Name)
                        .ToHashSet();
                }

                return Task.CompletedTask;
            });

            // MVC documents [FromQuery] record properties in PascalCase; the contract (and the frontend) uses camelCase.
            // Binding is case-insensitive, so this only changes the docs.
            options.AddOperationTransformer((operation, _, _) =>
            {
                foreach (var parameter in operation.Parameters?.OfType<OpenApiParameter>() ?? [])
                {
                    if (parameter.In == ParameterLocation.Query && parameter.Name is { Length: > 0 } name)
                    {
                        parameter.Name = JsonNamingPolicy.CamelCase.ConvertName(name);
                    }
                }

                return Task.CompletedTask;
            });
        });
        return services;
    }

    public static WebApplication UseAppOpenApi(this WebApplication app)
    {
        if (!app.Configuration.GetValue<bool>(EnabledKey))
        {
            return app;
        }

        app.MapOpenApi();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint(DocumentPath, "Task Manager API v1");
            // Same origin as the API, so the session cookie is sent automatically; only the CSRF header needs adding.
            options.UseRequestInterceptor(
                $"(req) => {{ req.headers['{CsrfHeaderMiddleware.HeaderName}'] = '{CsrfHeaderMiddleware.HeaderValue}'; return req; }}");
        });
        return app;
    }
}
