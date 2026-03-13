using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TM.Contracts.Tasks;
using TM.ServiceLogic.Interfaces;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class TasksController : ControllerBase
{
    private readonly ITaskService _taskService;
    public TasksController(ITaskService taskService) => _taskService = taskService;

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
        if (string.IsNullOrEmpty(userIdClaim)) return Unauthorized();

        var task = await _taskService.GetTaskByIdAsync(id, int.Parse(userIdClaim), userRole ?? "User");
        return task == null ? Forbid("Access denied or task deleted.") : Ok(task);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] TaskCreateRequest request)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim)) return Unauthorized();

        var result = await _taskService.CreateTaskAsync(request, int.Parse(userIdClaim));
        return Ok(result);
    }

    [HttpPatch("{id}/status")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] TaskStatusRequest request)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userRole = User.FindFirst(ClaimTypes.Role)?.Value ?? "User";
        if (string.IsNullOrEmpty(userIdClaim)) return Unauthorized();

        var success = await _taskService.UpdateStatusAsync(id, request.Status, int.Parse(userIdClaim), userRole);
        return !success ? Forbid("Cannot update status.") : NoContent();
    }

    [HttpGet("/api/Users/Tasks")]
    public async Task<IActionResult> GetAll()
    {
        int userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        string role = User.FindFirst(ClaimTypes.Role)!.Value;
        return Ok(await _taskService.GetAllTasksAsync(userId, role));
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(int id, [FromBody] TaskUpdateRequest request)
    {
        int userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var updatedTask = await _taskService.UpdateTaskAsync(id, request, userId);
        return updatedTask == null ? Forbid("Update failed.") : Ok(updatedTask);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        int userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var result = await _taskService.DeleteTaskAsync(id, userId);

        if (result == null) return Forbid("Task not found or not owner.");
        if (result == false) return BadRequest("Only 'Pending' tasks can be deleted.");

        return Ok(new { message = "Task soft-deleted successfully" });
    }
}