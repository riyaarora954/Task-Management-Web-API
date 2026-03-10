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
        // Get info from the token
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var userRole = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;

        if (string.IsNullOrEmpty(userIdClaim)) return Unauthorized();

        int currentUserId = int.Parse(userIdClaim);

        // Call service with the extra security info
        var task = await _taskService.GetTaskByIdAsync(id, currentUserId, userRole ?? "User");

        if (task == null)
        {
            // We return 'Forbid' so they know they exist but aren't allowed to see it
            return Forbid("You are not the creator (Admin) or the assigned user for this task.");
        }

        return Ok(task);
    }

    [Authorize(Roles = "Admin")] // Only Admins can enter this "Room"
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] TaskCreateRequest request)
    {
        // 1. Extract the ID automatically from the JWT NameIdentifier claim
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userIdClaim)) return Unauthorized();

        int adminId = int.Parse(userIdClaim);

        // 2. Pass the extracted adminId to the service
        var result = await _taskService.CreateTaskAsync(request, adminId);

        return Ok(result);
    }

    [HttpPatch("{id}/status")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] TaskStatusRequest request)
    {
        // Extract info from the logged-in user's token
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var userRole = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "User";

        if (string.IsNullOrEmpty(userIdClaim)) return Unauthorized();

        int currentUserId = int.Parse(userIdClaim);

        // Pass the user info for the security check
        var success = await _taskService.UpdateStatusAsync(id, request.Status, currentUserId, userRole);

        if (!success)
        {
            return Forbid("You do not have permission to update the status of this task.");
        }

        return NoContent();
    }
    [HttpGet("/api/Users/Tasks")]
    public async Task<IActionResult> GetAll()
    {
        // Extract ID and Role from the JWT Token
        int userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        string role = User.FindFirst(ClaimTypes.Role)!.Value;

        var tasks = await _taskService.GetAllTasksAsync(userId, role);
        return Ok(tasks);
    }

    // 2. UPDATE TASK (Only for Admin who created it)
    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(int id, [FromBody] TaskUpdateRequest request)
    {
        int userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        var updatedTask = await _taskService.UpdateTaskAsync(id, request, userId);

        if (updatedTask == null)
            return Forbid("You can only update tasks that you created.");

        return Ok(updatedTask);
    }

    // 3. DELETE TASK (Only for Admin who created it)
    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        int userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        var success = await _taskService.DeleteTaskAsync(id, userId);

        if (!success)
            return Forbid("You can only delete tasks that you created.");

        return Ok(new { message = "Task deleted successfully" });
    }
}