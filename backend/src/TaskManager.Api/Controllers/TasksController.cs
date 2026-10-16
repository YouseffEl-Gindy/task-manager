using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskManager.Api.Auth;
using TaskManager.Api.Dtos.Common;
using TaskManager.Api.Dtos.Tasks;
using TaskManager.Api.Services;

namespace TaskManager.Api.Controllers;

/// <summary>API.md 5.8 to 5.12. A malformed id fails the <c>guid</c> route constraint and becomes <c>404 not_found</c>.</summary>
[ApiController]
[Authorize]
[Route("api/v1/tasks")]
[Produces("application/json")]
[ProducesResponseType<ApiError>(StatusCodes.Status401Unauthorized)]
public sealed class TasksController : ControllerBase
{
    private readonly ITaskService _tasks;

    public TasksController(ITaskService tasks)
    {
        _tasks = tasks;
    }

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<TaskDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<TaskDto>>> List([FromQuery] TaskListQuery query, CancellationToken cancellationToken) =>
        Ok(await _tasks.ListAsync(User.GetUserId(), query, cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<TaskDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var task = await _tasks.GetAsync(User.GetUserId(), id, cancellationToken);
        return task is null ? ApiErrorResults.NotFound(HttpContext) : Ok(task);
    }

    [HttpPost]
    [ProducesResponseType<TaskDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateTaskRequest request, CancellationToken cancellationToken)
    {
        var result = await _tasks.CreateAsync(User.GetUserId(), request, cancellationToken);
        if (result.Outcome != ServiceOutcome.Success)
        {
            return this.ToErrorResult(result.Outcome, result.Errors);
        }

        // Relative Location, because the API is reached through the Next.js proxy.
        return Created($"/api/v1/tasks/{result.Value!.Id}", result.Value);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<TaskDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTaskRequest request, CancellationToken cancellationToken)
    {
        var result = await _tasks.UpdateAsync(User.GetUserId(), id, request, cancellationToken);
        return result.Outcome == ServiceOutcome.Success
            ? Ok(result.Value)
            : this.ToErrorResult(result.Outcome, result.Errors);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken) =>
        await _tasks.DeleteAsync(User.GetUserId(), id, cancellationToken)
            ? NoContent()
            : ApiErrorResults.NotFound(HttpContext);
}
