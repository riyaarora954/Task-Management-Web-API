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

        // Admins can comment on their own tasks, and assigned users can comment on tasks assigned to them.
        public async Task<CommentResponse?> AddCommentAsync(CommentRequest request, int currentUserId, string role)
        {
            var task = await _context.Tasks.FirstOrDefaultAsync(t => t.Id == request.TaskId && !t.IsDeleted);
            if (task == null) return null;

            bool isCreator = (role == "Admin" && task.CreatedBy == currentUserId);
            bool isAssigned = (task.AssignedToUserId == currentUserId);

            if (!isCreator && !isAssigned) return null;

            var comment = new Comment
            {
                Content = request.Content,
                TaskId = request.TaskId,
                UserId = currentUserId,
                CreatedAt = DateTime.UtcNow,
                IsDeleted = false
            };

            _context.Comments.Add(comment);
            await _context.SaveChangesAsync();

            var savedComment = await _context.Comments.Include(c => c.User).FirstAsync(c => c.Id == comment.Id);
            return _mapper.Map<CommentResponse>(savedComment);
        }

        // Admins can delete their own comments, and assigned users can delete their own comments on tasks assigned to them.
        public async Task<bool?> DeleteCommentAsync(int commentId, int currentUserId, string role)
        {
            var comment = await _context.Comments.Include(c => c.Task).FirstOrDefaultAsync(c => c.Id == commentId && !c.IsDeleted);

            if (comment == null || comment.Task == null || comment.Task.IsDeleted) return null;

            bool isAuthor = (comment.UserId == currentUserId);
            bool isTaskCreatorAdmin = (role == "Admin" && comment.Task?.CreatedBy == currentUserId);

            if (isAuthor || isTaskCreatorAdmin)
            {
                comment.IsDeleted = true;
                await _context.SaveChangesAsync();
                return true;
            }
            return false;
        }

        // Admins can view comments on their own tasks, and assigned users can view comments on tasks assigned to them.
        public async Task<IEnumerable<CommentResponse>?> GetCommentsByTaskIdAsync(int taskId, int currentUserId, string role)
        {
            var task = await _context.Tasks.FirstOrDefaultAsync(t => t.Id == taskId);
            if (task == null || task.IsDeleted) return null;

            bool isCreator = (role == "Admin" && task.CreatedBy == currentUserId);
            bool isAssigned = (task.AssignedToUserId == currentUserId);

            if (!isCreator && !isAssigned) throw new UnauthorizedAccessException();

            var comments = await _context.Comments
                .Include(c => c.User)
                .Where(c => c.TaskId == taskId && !c.IsDeleted)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();

            return _mapper.Map<IEnumerable<CommentResponse>>(comments);
        }

        // Admins can update their own comments, and assigned users can update their own comments on tasks assigned to them.
        public async Task<CommentResponse?> UpdateCommentAsync(int commentId, CommentUpdateRequest request, int currentUserId)
        {
            var comment = await _context.Comments
                .Include(c => c.User)
                .Include(c => c.Task)
                .FirstOrDefaultAsync(c => c.Id == commentId && !c.IsDeleted);

            if (comment == null || comment.Task == null || comment.Task.IsDeleted) return null;

            if (comment.UserId != currentUserId) return null;

            comment.Content = request.Content;
            await _context.SaveChangesAsync();
            return _mapper.Map<CommentResponse>(comment);
        }
    }
}