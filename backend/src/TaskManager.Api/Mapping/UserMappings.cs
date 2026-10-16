using TaskManager.Api.Dtos.Auth;
using TaskManager.Api.Entities;

namespace TaskManager.Api.Mapping;

public static class UserMappings
{
    public static UserDto ToDto(this User user) =>
        new(user.Id, user.Email ?? string.Empty, user.DisplayName, TaskMappings.AsUtc(user.CreatedAt));
}
