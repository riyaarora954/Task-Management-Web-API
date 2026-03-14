using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Security.Claims;
using System.Threading.Tasks;
using TM.Contracts.Tasks;
using TM.ServiceLogic.Interfaces;

namespace Task_Management.Controllers
{
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
                // Safety check for claims to resolve CS8602
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim)) return Unauthorized(new { message = "User identity not found." });

                var userRole = User.FindFirst(ClaimTypes.Role)?.Value ?? "User";
                int currentUserId = int.Parse(userIdClaim);

                var task = await _taskService.GetTaskByIdAsync(id, currentUserId, userRole);

                if (task == null)
                {
                    return StatusCode(403, new { message = "You are not authorized to view this task or it does not exist." });
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
        public async Task<IActionResult> Create([FromBody] TaskCreateRequest request)
        {
            try
            {
                if (!User.IsInRole("Admin"))
                {
                    return StatusCode(403, new { message = "Only Administrators can create tasks." });
                }

                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim)) return Unauthorized(new { message = "User identity not found." });

                int adminId = int.Parse(userIdClaim);
                var result = await _taskService.CreateTaskAsync(request, adminId);

                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPatch("{id}/status")]
        public async Task<IActionResult> UpdateStatus(int id, [FromBody] TaskStatusRequest request)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim)) return Unauthorized(new { message = "User identity not found." });

                var userRole = User.FindFirst(ClaimTypes.Role)?.Value ?? "User";
                int userId = int.Parse(userIdClaim);

                var success = await _taskService.UpdateStatusAsync(id, request.Status, userId, userRole);

                // FIX: Replaced Forbid() with StatusCode(403) to prevent the "clumsy" crash
                if (!success)
                {
                    return StatusCode(403, new { message = "You do not have permission to update this status or the task is deleted." });
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error updating status.", error = ex.Message });
            }
        }

        [HttpGet("/api/Users/Tasks")]
        public async Task<IActionResult> GetAll()
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim)) return Unauthorized(new { message = "User identity not found." });

                var roleClaim = User.FindFirst(ClaimTypes.Role)?.Value ?? "User";
                int userId = int.Parse(userIdClaim);

                return Ok(await _taskService.GetAllTasksAsync(userId, roleClaim));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error fetching tasks.", error = ex.Message });
            }
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Update(int id, [FromBody] TaskUpdateRequest request)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim)) return Unauthorized(new { message = "User identity not found." });

                int userId = int.Parse(userIdClaim);
                var result = await _taskService.UpdateTaskAsync(id, request, userId);
                return Ok(result);
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { message = "The task does not exist or has been deleted." });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred.", error = ex.Message });
            }
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim)) return Unauthorized(new { message = "User identity not found." });

                int userId = int.Parse(userIdClaim);
                var result = await _taskService.DeleteTaskAsync(id, userId);

                if (result == null)
                    return NotFound(new { message = "The task does not exist or has been already deleted." });

                if (result == false)
                    return StatusCode(403, new { message = "Only the task creator can delete this task." });

                return Ok(new { message = "Task and associated comments deleted successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred.", error = ex.Message });
            }
        }
    }
}