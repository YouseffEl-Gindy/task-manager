using System.Text.Json;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace TaskManager.Api.Validation;

/// <summary>
/// Binds enum query values by name only (camelCase names, case-insensitive). Numeric strings and undefined
/// names are rejected with a model error, which becomes <c>400 validation_failed</c>. An empty value means "not sent".
/// </summary>
public sealed class StrictEnumModelBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        var result = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);
        if (result == ValueProviderResult.None)
        {
            return Task.CompletedTask;
        }

        bindingContext.ModelState.SetModelValue(bindingContext.ModelName, result);
        var text = result.FirstValue?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return Task.CompletedTask;
        }

        var enumType = Nullable.GetUnderlyingType(bindingContext.ModelType) ?? bindingContext.ModelType;
        if (StrictEnumNames.TryParse(enumType, text, out var value))
        {
            bindingContext.Result = ModelBindingResult.Success(value);
        }
        else
        {
            bindingContext.ModelState.TryAddModelError(
                bindingContext.ModelName,
                $"The value is not valid. Allowed values: {string.Join(", ", StrictEnumNames.JsonNames(enumType))}.");
        }

        return Task.CompletedTask;
    }
}

public sealed class StrictEnumModelBinderProvider : IModelBinderProvider
{
    public IModelBinder? GetBinder(ModelBinderProviderContext context)
    {
        var type = Nullable.GetUnderlyingType(context.Metadata.ModelType) ?? context.Metadata.ModelType;
        return type.IsEnum ? new StrictEnumModelBinder() : null;
    }
}

/// <summary>Enum name matching shared by query binding: only defined names, never numbers.</summary>
public static class StrictEnumNames
{
    public static IEnumerable<string> JsonNames(Type enumType) =>
        Enum.GetNames(enumType).Select(n => JsonNamingPolicy.CamelCase.ConvertName(n));

    public static bool TryParse(Type enumType, string text, out object? value)
    {
        value = null;
        var name = Enum.GetNames(enumType).FirstOrDefault(n => string.Equals(n, text, StringComparison.OrdinalIgnoreCase));
        if (name is null)
        {
            return false;
        }

        value = Enum.Parse(enumType, name);
        return true;
    }
}
