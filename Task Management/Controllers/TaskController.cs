using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var tasks = await _taskService.GetAllTasksAsync();
        return Ok(tasks);
    }

    // 2. UPDATE TASK (Title, Description, etc.)
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] TaskUpdateRequest request)
    {
        var updatedTask = await _taskService.UpdateTaskAsync(id, request);

        if (updatedTask == null)
            return NotFound($"Task with ID {id} not found.");

        return Ok(updatedTask);
    }

    // 3. DELETE TASK (Only Admins)
    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")] // <--- THIS is the security guard
    public async Task<IActionResult> Delete(int id)
    {
        var success = await _taskService.DeleteTaskAsync(id);

        if (!success)
            return NotFound($"Task with ID {id} not found.");

        return Ok(); // Success (204)
    }
}