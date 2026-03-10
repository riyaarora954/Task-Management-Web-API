
using TM.Contracts.Comments;

namespace TM.ServiceLogic.Interfaces
{
    public interface ICommentService
    {
        Task<CommentResponse?> AddCommentAsync(CommentRequest request, int currentUserId, string role);
        Task<bool?> DeleteCommentAsync(int commentId, int currentUserId, string role);
    }
}