using TaskManager.Api.Dtos.Tasks;

namespace TaskManager.Api.Validation;

/// <summary>Field rules plus the create-time due-date rule (strictly later than the server's current UTC time).</summary>
public sealed class CreateTaskRequestValidator : IRequestValidator<CreateTaskRequest>
{
    private readonly TimeProvider _timeProvider;

    public CreateTaskRequestValidator(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public ValidationErrors Validate(CreateTaskRequest request)
    {
        var errors = new ValidationErrors();
        FieldRules.Title(errors, request.Title);
        FieldRules.Description(errors, request.Description);

        var dueUtc = DueDateRules.ToUtcWholeSeconds(request.DueDate);
        if (!DueDateRules.IsAllowedOnCreate(dueUtc, _timeProvider.GetUtcNow().UtcDateTime))
        {
            errors.Add(DueDateRules.Field, DueDateRules.MustBeInFutureMessage);
        }

        return errors;
    }
}
