using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Security.Claims;
using TM.Contracts.Comments;
using TM.ServiceLogic.Interfaces;

namespace Task_Management.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class CommentsController : ControllerBase
    {
        private readonly ICommentService _commentService;
        private readonly ILogger<CommentsController> _logger;

        public CommentsController(ICommentService commentService, ILogger<CommentsController> logger)
        {
            _commentService = commentService;
            _logger = logger;
        }

        // Create Endpoint
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CommentRequest request)
        {
            var sw = Stopwatch.StartNew();

            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim)) return Unauthorized(new { message = "User identity not found." });

                int userId = int.Parse(userIdClaim);
                var role = User.FindFirst(ClaimTypes.Role)?.Value ?? "User";

                _logger.LogInformation("[CommentsController] POST comments | UserId={UserId} Role={Role} TaskId={TaskId}", userId, role, request.TaskId);

                var result = await _commentService.AddCommentAsync(request, userId, role);

                sw.Stop();

                if (result == null)
                {
                    _logger.LogWarning("[CommentsController] POST comments | 403 FORBIDDEN UserId={UserId} TaskId={TaskId} | {Elapsed}ms", userId, request.TaskId, sw.ElapsedMilliseconds);
                    return StatusCode(403, new { message = "Cannot add comment. Task may be deleted or you do not have permission." });
                }

                _logger.LogInformation("[CommentsController] POST comments | 200 OK CommentId={CommentId} TaskId={TaskId} | {Elapsed}ms", result.Id, request.TaskId, sw.ElapsedMilliseconds);
                return Ok(result);
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger.LogError(ex, "[CommentsController] POST comments | 500 ERROR TaskId={TaskId} | {Elapsed}ms", request.TaskId, sw.ElapsedMilliseconds);
                return StatusCode(500, new { message = "Error creating comment.", error = ex.Message });
            }
        }

        // Delete Endpoint
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var sw = Stopwatch.StartNew();

            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim)) return Unauthorized(new { message = "User identity not found." });

                int userId = int.Parse(userIdClaim);
                var role = User.FindFirst(ClaimTypes.Role)?.Value ?? "User";

                _logger.LogInformation("[CommentsController] DELETE comments/{CommentId} | UserId={UserId} Role={Role}", id, userId, role);

                var result = await _commentService.DeleteCommentAsync(id, userId, role);

                sw.Stop();

                if (result == null)
                {
                    _logger.LogWarning("[CommentsController] DELETE comments/{CommentId} | 404 NOT FOUND | {Elapsed}ms", id, sw.ElapsedMilliseconds);
                    return NotFound(new { message = "Comment not found or the associated task has been deleted." });
                }

                if (result == false)
                {
                    _logger.LogWarning("[CommentsController] DELETE comments/{CommentId} | 403 FORBIDDEN UserId={UserId} not author | {Elapsed}ms", id, userId, sw.ElapsedMilliseconds);
                    return StatusCode(403, new { message = "You can only delete your own comments." });
                }

                _logger.LogInformation("[CommentsController] DELETE comments/{CommentId} | 204 NO CONTENT | {Elapsed}ms", id, sw.ElapsedMilliseconds);
                return NoContent();
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger.LogError(ex, "[CommentsController] DELETE comments/{CommentId} | 500 ERROR | {Elapsed}ms", id, sw.ElapsedMilliseconds);
                return StatusCode(500, new { message = "Error deleting comment.", error = ex.Message });
            }
        }

        // GetByTaskId Endpoint
        [HttpGet("task/{taskId}")]
        public async Task<IActionResult> GetByTaskId(int taskId)
        {
            var sw = Stopwatch.StartNew();

            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim)) return Unauthorized(new { message = "User identity not found." });

                int userId = int.Parse(userIdClaim);
                var role = User.FindFirst(ClaimTypes.Role)?.Value ?? "User";

                _logger.LogInformation("[CommentsController] GET comments/task/{TaskId} | UserId={UserId} Role={Role}", taskId, userId, role);

                var comments = await _commentService.GetCommentsByTaskIdAsync(taskId, userId, role);

                sw.Stop();

                if (comments == null)
                {
                    _logger.LogWarning("[CommentsController] GET comments/task/{TaskId} | 404 NOT FOUND | {Elapsed}ms", taskId, sw.ElapsedMilliseconds);
                    return NotFound(new { message = "The associated task does not exist or has been deleted." });
                }

                _logger.LogInformation("[CommentsController] GET comments/task/{TaskId} | 200 OK Count={Count} | {Elapsed}ms", taskId, comments.Count(), sw.ElapsedMilliseconds);
                return Ok(comments);
            }
            catch (UnauthorizedAccessException)
            {
                sw.Stop();
                _logger.LogWarning("[CommentsController] GET comments/task/{TaskId} | 403 FORBIDDEN | {Elapsed}ms", taskId, sw.ElapsedMilliseconds);
                return StatusCode(403, new { message = "You do not have permission to view comments for this task." });
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger.LogError(ex, "[CommentsController] GET comments/task/{TaskId} | 500 ERROR | {Elapsed}ms", taskId, sw.ElapsedMilliseconds);
                return StatusCode(500, new { message = "Error fetching comments.", error = ex.Message });
            }
        }

        // Update Endpoint
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] CommentUpdateRequest request)
        {
            var sw = Stopwatch.StartNew();

            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim)) return Unauthorized(new { message = "User identity not found." });

                int userId = int.Parse(userIdClaim);
                _logger.LogInformation("[CommentsController] PUT comments/{CommentId} | UserId={UserId}", id, userId);

                var result = await _commentService.UpdateCommentAsync(id, request, userId);

                sw.Stop();

                if (result == null)
                {
                    _logger.LogWarning("[CommentsController] PUT comments/{CommentId} | 404 NOT FOUND UserId={UserId} | {Elapsed}ms", id, userId, sw.ElapsedMilliseconds);
                    return NotFound(new { message = "Update failed. Comment/Task not found or you are not the author." });
                }

                _logger.LogInformation("[CommentsController] PUT comments/{CommentId} | 200 OK | {Elapsed}ms", id, sw.ElapsedMilliseconds);
                return Ok(result);
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger.LogError(ex, "[CommentsController] PUT comments/{CommentId} | 500 ERROR | {Elapsed}ms", id, sw.ElapsedMilliseconds);
                return StatusCode(500, new { message = "Error updating comment.", error = ex.Message });
            }
        }
    }
}