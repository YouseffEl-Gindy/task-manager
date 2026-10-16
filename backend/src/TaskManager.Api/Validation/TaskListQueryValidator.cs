using TaskManager.Api.Dtos.Tasks;

namespace TaskManager.Api.Validation;

/// <summary>Enum query values are checked by <see cref="StrictEnumModelBinder"/>; this checks <c>search</c>.</summary>
public sealed class TaskListQueryValidator : IRequestValidator<TaskListQuery>
{
    public ValidationErrors Validate(TaskListQuery request)
    {
        var errors = new ValidationErrors();
        var search = FieldRules.Trim(request.Search);
        if (search is not null && search.Length > FieldRules.SearchMaxLength)
        {
            errors.Add("search", $"Search must be at most {FieldRules.SearchMaxLength} characters.");
        }

        return errors;
    }
}
