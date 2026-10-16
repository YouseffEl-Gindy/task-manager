namespace TaskManager.Api.Dtos.Auth;

public sealed record RegisterRequest(string DisplayName, string Email, string Password);
