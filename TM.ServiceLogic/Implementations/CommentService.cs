using AutoMapper;
using Microsoft.EntityFrameworkCore;
using TM.Contracts.Comments;
using TM.Model.Data;
using TM.Model.Entities;
using TM.ServiceLogic.Interfaces;

namespace TM.ServiceLogic.Implementations
{
    public class CommentService : ICommentService
    {
        private readonly TMDbContext _context;
        private readonly IMapper _mapper;

        public CommentService(TMDbContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }

        public async Task<CommentResponse?> AddCommentAsync(CommentRequest request, int currentUserId, string role)
        {
            // 1. Fetch the task to check permissions
            var task = await _context.Tasks.FindAsync(request.TaskId);
            if (task == null) return null;

            // 🛡️ SECURITY CHECK: 
            // - Is it the Admin who created the task?
            // - OR Is it the User assigned to the task?
            bool isCreator = (role == "Admin" && task.CreatedBy == currentUserId);
            bool isAssigned = (task.AssignedToUserId == currentUserId);

            if (!isCreator && !isAssigned) return null;

            // 2. Create the comment
            var comment = new Comment
            {
                Content = request.Content,
                TaskId = request.TaskId,
                UserId = currentUserId, // The logged-in user is the author
                CreatedAt = DateTime.UtcNow
            };

            _context.Comments.Add(comment);
            await _context.SaveChangesAsync();

            // 3. Return response (Include User for the AuthorName)
            var savedComment = await _context.Comments
                .Include(c => c.User)
                .FirstAsync(c => c.Id == comment.Id);

            return _mapper.Map<CommentResponse>(savedComment);
        }
        public async Task<bool?> DeleteCommentAsync(int commentId, int currentUserId, string role)
        {
            // 1. Fetch comment and include the Task so we can see the Task Creator (Admin)
            var comment = await _context.Comments
                .Include(c => c.Task)
                .FirstOrDefaultAsync(c => c.Id == commentId);

            if (comment == null) return null; // 404 Not Found

            // 🛡️ THE AUTHORIZATION LOGIC:
            // A. Is this the person who wrote the comment? (User or Admin deleting their own)
            bool isAuthor = (comment.UserId == currentUserId);

            // B. Is this the Admin who created the task? (The "Moderator" power)
            bool isTaskCreatorAdmin = (role == "Admin" && comment.Task?.CreatedBy == currentUserId);

            if (isAuthor || isTaskCreatorAdmin)
            {
                _context.Comments.Remove(comment);
                await _context.SaveChangesAsync();
                return true; // Success
            }

            // If they aren't the author and aren't the task's Admin, they are blocked
            return false; // 403 Forbidden
        }


        // ADD THESE METHODS AT THE BOTTOM OF YOUR CommentService CLASS:

        public async Task<IEnumerable<CommentResponse>?> GetCommentsByTaskIdAsync(int taskId, int currentUserId, string role)
        {
            // 1. Find the task to verify who is allowed to see the comments
            var task = await _context.Tasks.FindAsync(taskId);
            if (task == null) return null;

            // 🛡️ AUTH CHECK: Matches teammate's style
            // Only the Admin who created the task OR the User assigned to the task can see the comments
            bool isCreator = (role == "Admin" && task.CreatedBy == currentUserId);
            bool isAssigned = (task.AssignedToUserId == currentUserId);

            if (!isCreator && !isAssigned) return null;

            // 2. Fetch and return comments
            var comments = await _context.Comments
                .Include(c => c.User)
                .Where(c => c.TaskId == taskId)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();

            return _mapper.Map<IEnumerable<CommentResponse>>(comments);
        }

        public async Task<CommentResponse?> UpdateCommentAsync(int commentId, CommentRequest request, int currentUserId)
        {
            var comment = await _context.Comments
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.Id == commentId);

            if (comment == null) return null;

            // 🛡️ AUTH CHECK: Strictly only the person who wrote the comment can edit it
            // Even an Admin cannot edit a user's specific comment text
            if (comment.UserId != currentUserId) return null;

            comment.Content = request.Content;

            await _context.SaveChangesAsync();
            return _mapper.Map<CommentResponse>(comment);
        }


    }
}