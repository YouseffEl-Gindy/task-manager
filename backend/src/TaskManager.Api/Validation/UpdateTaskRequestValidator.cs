using TaskManager.Api.Dtos.Tasks;

namespace TaskManager.Api.Validation;

/// <summary>Rules that don't need the stored task. The due-date rule runs after the ownership lookup (API.md 5.11).</summary>
public sealed class UpdateTaskRequestValidator : IRequestValidator<UpdateTaskRequest>
{
    public ValidationErrors Validate(UpdateTaskRequest request)
    {
        var errors = new ValidationErrors();
        FieldRules.Title(errors, request.Title);
        FieldRules.Description(errors, request.Description);
        return errors;
    }
}
