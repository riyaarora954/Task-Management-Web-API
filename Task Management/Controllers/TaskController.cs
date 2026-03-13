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
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
            int currentUserId = int.Parse(userIdClaim!);

            var task = await _taskService.GetTaskByIdAsync(id, currentUserId, userRole ?? "User");

            if (task == null)
            {
                // Instead of a generic error, we return a specific message
                return StatusCode(403, new { message = "You are not authorized to view this task." });
            }

            return Ok(task);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = "An error occurred: " + ex.Message });
        }
    }

    [HttpPost]
    [ProducesResponseType(typeof(TaskResponse), 200)]
    public async Task<IActionResult> Create([FromBody] TM.Contracts.Tasks.TaskCreateRequest request)
    {
        try
        {
            // Manual Role Check to provide custom message
            if (!User.IsInRole("Admin"))
            {
                return StatusCode(403, new { message = "Only Administrators can create tasks." });
            }

            int adminId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
            var result = await _taskService.CreateTaskAsync(request, adminId);

            return Ok(result); // Success: Returns Task object only
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
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
    // 2. DELETE TASK FIX (The one from your screenshot)
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            // Manual Role Check
            if (!User.IsInRole("Admin"))
            {
                return StatusCode(403, new { message = "You do not have permission to delete tasks. Admin role required." });
            }

            int userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
            var success = await _taskService.DeleteTaskAsync(id, userId);

            if (!success)
            {
                // This handles if an Admin tries to delete a task they didn't create
                return StatusCode(403, new { message = "You can only delete tasks that you personally created." });
            }

            return Ok(new { message = "Task deleted successfully" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}