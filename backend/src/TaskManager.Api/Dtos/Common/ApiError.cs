namespace TaskManager.Api.Dtos.Common;

public sealed record ApiError(
    int Status,
    string Code,                                   // one of ApiErrorCodes
    string Message,
    IReadOnlyDictionary<string, string[]>? Errors, // non-null only for validation_failed
    string TraceId);
