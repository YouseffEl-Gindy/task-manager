namespace TaskManager.Api.Dtos.Account;

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
