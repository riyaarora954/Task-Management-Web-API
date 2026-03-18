using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Security.Claims;
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
        private readonly ILogger<TasksController> _logger;

        public TasksController(ITaskService taskService, ILogger<TasksController> logger)
        {
            _taskService = taskService;
            _logger = logger;
        }

        // GetById Endpoint
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var sw = Stopwatch.StartNew();

            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim)) return Unauthorized(new { message = "User identity not found." });

                var userRole = User.FindFirst(ClaimTypes.Role)?.Value ?? "User";
                int currentUserId = int.Parse(userIdClaim);

                _logger.LogInformation("[TasksController] GET tasks/{TaskId} | UserId={UserId} Role={Role}", id, currentUserId, userRole);

                var task = await _taskService.GetTaskByIdAsync(id, currentUserId, userRole);

                sw.Stop();

                if (task == null)
                {
                    _logger.LogWarning("[TasksController] GET tasks/{TaskId} | 403 FORBIDDEN UserId={UserId} | {Elapsed}ms", id, currentUserId, sw.ElapsedMilliseconds);
                    return StatusCode(403, new { message = "You are not authorized to view this task or it does not exist." });
                }

                _logger.LogInformation("[TasksController] GET tasks/{TaskId} | 200 OK | {Elapsed}ms", id, sw.ElapsedMilliseconds);
                return Ok(task);
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger.LogError(ex, "[TasksController] GET tasks/{TaskId} | 400 ERROR | {Elapsed}ms", id, sw.ElapsedMilliseconds);
                return BadRequest(new { message = "An error occurred: " + ex.Message });
            }
        }

        // Create Endpoint
        [HttpPost]
        [ProducesResponseType(typeof(TaskResponse), 200)]
        public async Task<IActionResult> Create([FromBody] TaskCreateRequest request)
        {
            var sw = Stopwatch.StartNew();

            try
            {
                if (!User.IsInRole("Admin"))
                {
                    sw.Stop();
                    _logger.LogWarning("[TasksController] POST tasks | 403 FORBIDDEN non-admin attempted task creation | {Elapsed}ms", sw.ElapsedMilliseconds);
                    return StatusCode(403, new { message = "Only Administrators can create tasks." });
                }

                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim)) return Unauthorized(new { message = "User identity not found." });

                int adminId = int.Parse(userIdClaim);
                _logger.LogInformation("[TasksController] POST tasks | AdminId={AdminId} AssignedTo={AssignedTo}", adminId, request.AssignedToUserId);

                var result = await _taskService.CreateTaskAsync(request, adminId);

                sw.Stop();
                _logger.LogInformation("[TasksController] POST tasks | 200 OK TaskId={TaskId} AdminId={AdminId} | {Elapsed}ms", result.Id, adminId, sw.ElapsedMilliseconds);
                return Ok(result);
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger.LogError(ex, "[TasksController] POST tasks | 400 ERROR | {Elapsed}ms", sw.ElapsedMilliseconds);
                return BadRequest(new { message = ex.Message });
            }
        }

        // UpdateStatus Endpoint
        [HttpPatch("{id}/status")]
        public async Task<IActionResult> UpdateStatus(int id, [FromBody] TaskStatusRequest request)
        {
            var sw = Stopwatch.StartNew();

            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim)) return Unauthorized(new { message = "User identity not found." });

                var userRole = User.FindFirst(ClaimTypes.Role)?.Value ?? "User";
                int userId = int.Parse(userIdClaim);

                _logger.LogInformation("[TasksController] PATCH tasks/{TaskId}/status | NewStatus={Status} UserId={UserId} Role={Role}", id, request.Status, userId, userRole);

                var success = await _taskService.UpdateStatusAsync(id, request.Status, userId, userRole);

                sw.Stop();

                if (!success)
                {
                    _logger.LogWarning("[TasksController] PATCH tasks/{TaskId}/status | 403 FORBIDDEN UserId={UserId} | {Elapsed}ms", id, userId, sw.ElapsedMilliseconds);
                    return StatusCode(403, new { message = "You do not have permission to update this status or the task is deleted." });
                }

                _logger.LogInformation("[TasksController] PATCH tasks/{TaskId}/status | 204 NO CONTENT | {Elapsed}ms", id, sw.ElapsedMilliseconds);
                return NoContent();
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger.LogError(ex, "[TasksController] PATCH tasks/{TaskId}/status | 500 ERROR | {Elapsed}ms", id, sw.ElapsedMilliseconds);
                return StatusCode(500, new { message = "Error updating status.", error = ex.Message });
            }
        }

        // GetAll Endpoint
        [HttpGet("/api/Users/Tasks")]
        public async Task<IActionResult> GetAll()
        {
            var sw = Stopwatch.StartNew();

            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim)) return Unauthorized(new { message = "User identity not found." });

                var roleClaim = User.FindFirst(ClaimTypes.Role)?.Value ?? "User";
                int userId = int.Parse(userIdClaim);

                _logger.LogInformation("[TasksController] GET /api/Users/Tasks | UserId={UserId} Role={Role}", userId, roleClaim);

                var tasks = await _taskService.GetAllTasksAsync(userId, roleClaim);

                sw.Stop();
                _logger.LogInformation("[TasksController] GET /api/Users/Tasks | 200 OK Count={Count} UserId={UserId} | {Elapsed}ms", tasks.Count(), userId, sw.ElapsedMilliseconds);
                return Ok(tasks);
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger.LogError(ex, "[TasksController] GET /api/Users/Tasks | 500 ERROR | {Elapsed}ms", sw.ElapsedMilliseconds);
                return StatusCode(500, new { message = "Error fetching tasks.", error = ex.Message });
            }
        }

        // Update Endpoint
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Update(int id, [FromBody] TaskUpdateRequest request)
        {
            var sw = Stopwatch.StartNew();

            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim)) return Unauthorized(new { message = "User identity not found." });

                int userId = int.Parse(userIdClaim);
                _logger.LogInformation("[TasksController] PUT tasks/{TaskId} | UserId={UserId}", id, userId);

                var result = await _taskService.UpdateTaskAsync(id, request, userId);

                sw.Stop();
                _logger.LogInformation("[TasksController] PUT tasks/{TaskId} | 200 OK | {Elapsed}ms", id, sw.ElapsedMilliseconds);
                return Ok(result);
            }
            catch (KeyNotFoundException)
            {
                sw.Stop();
                _logger.LogWarning("[TasksController] PUT tasks/{TaskId} | 404 NOT FOUND | {Elapsed}ms", id, sw.ElapsedMilliseconds);
                return NotFound(new { message = "The task does not exist or has been deleted." });
            }
            catch (UnauthorizedAccessException ex)
            {
                sw.Stop();
                _logger.LogWarning("[TasksController] PUT tasks/{TaskId} | 403 FORBIDDEN {Message} | {Elapsed}ms", id, ex.Message, sw.ElapsedMilliseconds);
                return StatusCode(403, new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                sw.Stop();
                _logger.LogWarning("[TasksController] PUT tasks/{TaskId} | 400 BAD REQUEST {Message} | {Elapsed}ms", id, ex.Message, sw.ElapsedMilliseconds);
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger.LogError(ex, "[TasksController] PUT tasks/{TaskId} | 500 ERROR | {Elapsed}ms", id, sw.ElapsedMilliseconds);
                return StatusCode(500, new { message = "An error occurred.", error = ex.Message });
            }
        }

        // Delete Endpoint
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var sw = Stopwatch.StartNew();

            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim)) return Unauthorized(new { message = "User identity not found." });

                int userId = int.Parse(userIdClaim);
                _logger.LogInformation("[TasksController] DELETE tasks/{TaskId} | UserId={UserId}", id, userId);

                var result = await _taskService.DeleteTaskAsync(id, userId);

                sw.Stop();

                if (result == null)
                {
                    _logger.LogWarning("[TasksController] DELETE tasks/{TaskId} | 404 NOT FOUND | {Elapsed}ms", id, sw.ElapsedMilliseconds);
                    return NotFound(new { message = "The task does not exist or has been already deleted." });
                }

                if (result == false)
                {
                    _logger.LogWarning("[TasksController] DELETE tasks/{TaskId} | 403 FORBIDDEN UserId={UserId} not creator | {Elapsed}ms", id, userId, sw.ElapsedMilliseconds);
                    return StatusCode(403, new { message = "Only the task creator can delete this task." });
                }

                _logger.LogInformation("[TasksController] DELETE tasks/{TaskId} | 200 OK | {Elapsed}ms", id, sw.ElapsedMilliseconds);
                return Ok(new { message = "Task and associated comments deleted successfully." });
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger.LogError(ex, "[TasksController] DELETE tasks/{TaskId} | 500 ERROR | {Elapsed}ms", id, sw.ElapsedMilliseconds);
                return StatusCode(500, new { message = "An error occurred.", error = ex.Message });
            }
        }
    }
}