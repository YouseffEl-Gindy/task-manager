using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using TaskManager.Api.Controllers;

namespace TaskManager.Api.Validation;

/// <summary>
/// Runs the registered <see cref="IRequestValidator{T}"/> for each action argument before the action executes,
/// so request validation (400) always comes before the ownership lookup (404).
/// </summary>
public sealed class RequestValidationFilter : IAsyncActionFilter
{
    private static readonly ConcurrentDictionary<Type, (Type ValidatorType, MethodInfo Validate)> Cache = new();

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var errors = new ValidationErrors();
        foreach (var parameter in context.ActionDescriptor.Parameters)
        {
            context.ActionArguments.TryGetValue(parameter.Name, out var value);
            if (value is null)
            {
                if (parameter.BindingInfo?.BindingSource == BindingSource.Body)
                {
                    errors.Add(ModelStateErrorMapper.BodyKey, "A request body is required.");
                }

                continue;
            }

            var (validatorType, validate) = Cache.GetOrAdd(parameter.ParameterType, static type =>
            {
                var validatorType = typeof(IRequestValidator<>).MakeGenericType(type);
                return (validatorType, validatorType.GetMethod(nameof(IRequestValidator<object>.Validate))!);
            });

            var validator = context.HttpContext.RequestServices.GetService(validatorType);
            if (validator is null)
            {
                continue;
            }

            var result = (ValidationErrors)validate.Invoke(validator, [value])!;
            foreach (var (field, messages) in result.ToDictionary())
            {
                foreach (var message in messages)
                {
                    errors.Add(field, message);
                }
            }
        }

        if (!errors.IsValid)
        {
            context.Result = ApiErrorResults.Validation(context.HttpContext, errors);
            return;
        }

        await next();
    }
}
