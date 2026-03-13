using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

        public CommentsController(ICommentService commentService)
        {
            _commentService = commentService;
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CommentRequest request)
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
                var role = User.FindFirst(ClaimTypes.Role)?.Value ?? "User";

                var result = await _commentService.AddCommentAsync(request, userId, role);

                if (result == null)
                {
                    return Forbid("Only the task creator (Admin) or the assigned user can comment.");
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred while creating the comment: {ex.Message}");
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var role = User.FindFirst(ClaimTypes.Role)?.Value ?? "User";

                if (string.IsNullOrEmpty(userIdClaim)) return Unauthorized();
                int currentUserId = int.Parse(userIdClaim);

                var result = await _commentService.DeleteCommentAsync(id, currentUserId, role);

                if (result == null)
                    return NotFound("Comment not found.");

                if (result == false)
                    return Forbid("You can only delete your own comments, unless you are the Admin who created this task.");

                // CHANGE THIS LINE:
                return Ok(new { message = "Comment deleted successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred while deleting the comment: {ex.Message}");
            }
        }

        [HttpGet("task/{taskId}")]
        public async Task<IActionResult> GetByTaskId(int taskId)
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
                var role = User.FindFirst(ClaimTypes.Role)?.Value ?? "User";

                var comments = await _commentService.GetCommentsByTaskIdAsync(taskId, userId, role);

                if (comments == null)
                {
                    return Forbid("You do not have permission to view comments for this task.");
                }

                return Ok(comments);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred while fetching comments: {ex.Message}");
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] CommentRequest request)
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);

                var result = await _commentService.UpdateCommentAsync(id, request, userId);

                if (result == null)
                {
                    return Forbid("You can only edit your own comments.");
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred while updating the comment: {ex.Message}");
            }
        }
    }
}