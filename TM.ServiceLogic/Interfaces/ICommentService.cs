
using TM.Contracts.Comments;

namespace TM.ServiceLogic.Interfaces
{
    public interface ICommentService
    {
        Task<CommentResponse?> AddCommentAsync(CommentRequest request, int currentUserId, string role);
        Task<bool?> DeleteCommentAsync(int commentId, int currentUserId, string role);

        // ADD THESE TO YOUR EXISTING INTERFACE:
        Task<IEnumerable<CommentResponse>?> GetCommentsByTaskIdAsync(int taskId, int currentUserId, string role);
        Task<CommentResponse?> UpdateCommentAsync(int commentId, CommentRequest request, int currentUserId);
    }
}