namespace TaskManager.Api.Dtos.Auth;

public sealed record UserDto(Guid Id, string Email, string DisplayName, DateTime CreatedAt);
