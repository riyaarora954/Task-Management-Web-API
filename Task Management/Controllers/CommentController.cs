using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Security.Claims;
using System.Threading.Tasks;
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

        //Create Endpoint
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CommentRequest request)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim)) return Unauthorized(new { message = "User identity not found." });

                int userId = int.Parse(userIdClaim);
                var role = User.FindFirst(ClaimTypes.Role)?.Value ?? "User";

                var result = await _commentService.AddCommentAsync(request, userId, role);

                if (result == null)
                {
                    return StatusCode(403, new { message = "Cannot add comment. Task may be deleted or you do not have permission." });
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error creating comment.", error = ex.Message });
            }
        }

        //Delete Endpoint
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim)) return Unauthorized(new { message = "User identity not found." });

                int userId = int.Parse(userIdClaim);
                var role = User.FindFirst(ClaimTypes.Role)?.Value ?? "User";

                var result = await _commentService.DeleteCommentAsync(id, userId, role);

                if (result == null)
                    return NotFound(new { message = "Comment not found or the associated task has been deleted." });

                if (result == false)
                    return StatusCode(403, new { message = "You can only delete your own comments." });

                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error deleting comment.", error = ex.Message });
            }
        }

        //GetByTaskId Endpoint
        [HttpGet("task/{taskId}")]
        public async Task<IActionResult> GetByTaskId(int taskId)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim)) return Unauthorized(new { message = "User identity not found." });

                int userId = int.Parse(userIdClaim);
                var role = User.FindFirst(ClaimTypes.Role)?.Value ?? "User";

                var comments = await _commentService.GetCommentsByTaskIdAsync(taskId, userId, role);

                if (comments == null)
                {
                    return NotFound(new { message = "The associated task does not exist or has been deleted." });
                }

                return Ok(comments);
            }
            catch (UnauthorizedAccessException)
            {
                return StatusCode(403, new { message = "You do not have permission to view comments for this task." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error fetching comments.", error = ex.Message });
            }
        }

        //Update Endpoint
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] CommentUpdateRequest request)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim)) return Unauthorized(new { message = "User identity not found." });

                int userId = int.Parse(userIdClaim);

                var result = await _commentService.UpdateCommentAsync(id, request, userId);

                if (result == null)
                {
                    return NotFound(new { message = "Update failed. Comment/Task not found or you are not the author." });
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error updating comment.", error = ex.Message });
            }
        }
    }
}