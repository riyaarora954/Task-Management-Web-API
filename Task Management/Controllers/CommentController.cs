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
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
            var role = User.FindFirst(ClaimTypes.Role)?.Value ?? "User";

            var result = await _commentService.AddCommentAsync(request, userId, role);

            if (result == null)
            {
                return Forbid("Only the task creator (Admin) or the assigned user can comment.");
            }

            return Ok(result);
        }
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            // 🔍 Extract identity from JWT
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "User";

            if (string.IsNullOrEmpty(userIdClaim)) return Unauthorized();
            int currentUserId = int.Parse(userIdClaim);

            // Call service
            var result = await _commentService.DeleteCommentAsync(id, currentUserId, role);

            if (result == null)
                return NotFound("Comment not found.");

            if (result == false)
                return Forbid("You can only delete your own comments, unless you are the Admin who created this task.");

            return NoContent(); // Success!
        }



        // ADD THESE ENDPOINTS TO YOUR EXISTING CommentsController CLASS:

        [HttpGet("task/{taskId}")]
        public async Task<IActionResult> GetByTaskId(int taskId)
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

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] CommentRequest request)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);

            var result = await _commentService.UpdateCommentAsync(id, request, userId);

            if (result == null)
            {
                return Forbid("You can only edit your own comments.");
            }

            return Ok(result);
        }


    }
}