using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TaskManager.Api.Controllers;
using TaskManager.Api.Data;
using TaskManager.Api.Dtos.Account;
using TaskManager.Api.Dtos.Auth;
using TaskManager.Api.Dtos.Tasks;
using TaskManager.Api.Json;
using TaskManager.Api.Services;
using TaskManager.Api.Validation;

namespace TaskManager.Api;

public static class ApiServiceExtensions
{
    /// <summary>Controllers, JSON settings, validation, error shaping, app services and health checks.</summary>
    public static IServiceCollection AddAppApi(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);

        services.AddControllers(options =>
            {
                options.ModelBinderProviders.Insert(0, new StrictEnumModelBinderProvider());
                options.Filters.Add<RequestValidationFilter>();
                // Our validators produce camelCase keys; MVC's implicit [Required] would add PascalCase ones.
                options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
            })
            .AddJsonOptions(options => ApiJsonOptions.Apply(options.JsonSerializerOptions))
            .ConfigureApiBehaviorOptions(options =>
            {
                // Bare 404/405/415 etc. get an ApiError from the status-code fallback instead of ProblemDetails.
                options.SuppressMapClientErrors = true;
                options.InvalidModelStateResponseFactory = context =>
                {
                    var bodyParameterNames = context.ActionDescriptor.Parameters
                        .Where(p => p.BindingInfo?.BindingSource == BindingSource.Body)
                        .Select(p => p.Name)
                        .ToList();
                    return ApiErrorResults.Validation(
                        context.HttpContext,
                        ModelStateErrorMapper.Map(context.ModelState, bodyParameterNames));
                };
            });

        services.AddSingleton<IRequestValidator<RegisterRequest>, RegisterRequestValidator>();
        services.AddSingleton<IRequestValidator<LoginRequest>, LoginRequestValidator>();
        services.AddSingleton<IRequestValidator<UpdateProfileRequest>, UpdateProfileRequestValidator>();
        services.AddSingleton<IRequestValidator<ChangePasswordRequest>, ChangePasswordRequestValidator>();
        services.AddSingleton<IRequestValidator<DeleteAccountRequest>, DeleteAccountRequestValidator>();
        services.AddSingleton<IRequestValidator<CreateTaskRequest>, CreateTaskRequestValidator>();
        services.AddSingleton<IRequestValidator<UpdateTaskRequest>, UpdateTaskRequestValidator>();
        services.AddSingleton<IRequestValidator<TaskListQuery>, TaskListQueryValidator>();

        services.AddScoped<ITaskService, TaskService>();
        services.AddScoped<IAccountService, AccountService>();

        services.AddHealthChecks().AddDbContextCheck<AppDbContext>();
        return services;
    }
}
