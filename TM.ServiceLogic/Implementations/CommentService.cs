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

        public async Task<bool?> DeleteCommentAsync(int commentId, int currentUserId, string role)
        {
            var comment = await _context.Comments.Include(c => c.Task).FirstOrDefaultAsync(c => c.Id == commentId && !c.IsDeleted);
            if (comment == null) return null;

            bool isAuthor = (comment.UserId == currentUserId);
            bool isTaskCreatorAdmin = (role == "Admin" && comment.Task?.CreatedBy == currentUserId);

            if (isAuthor || isTaskCreatorAdmin)
            {
                comment.IsDeleted = true; // Soft delete - history remains
                await _context.SaveChangesAsync();
                return true;
            }
            return false;
        }

        public async Task<IEnumerable<CommentResponse>?> GetCommentsByTaskIdAsync(int taskId, int currentUserId, string role)
        {
            var task = await _context.Tasks.FirstOrDefaultAsync(t => t.Id == taskId && !t.IsDeleted);
            if (task == null) return null;

            bool isCreator = (role == "Admin" && task.CreatedBy == currentUserId);
            bool isAssigned = (task.AssignedToUserId == currentUserId);

            if (!isCreator && !isAssigned) return null;

            var comments = await _context.Comments
                .Include(c => c.User)
                .Where(c => c.TaskId == taskId && !c.IsDeleted)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();

            return _mapper.Map<IEnumerable<CommentResponse>>(comments);
        }

        public async Task<CommentResponse?> UpdateCommentAsync(int commentId, CommentRequest request, int currentUserId)
        {
            var comment = await _context.Comments.Include(c => c.User).FirstOrDefaultAsync(c => c.Id == commentId && !c.IsDeleted);
            if (comment == null || comment.UserId != currentUserId) return null;

            comment.Content = request.Content;
            await _context.SaveChangesAsync();
            return _mapper.Map<CommentResponse>(comment);
        }
    }
}