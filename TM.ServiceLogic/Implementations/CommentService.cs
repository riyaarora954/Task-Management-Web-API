using AutoMapper;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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

            // If comment is null OR the associated task is deleted, consider it "Not Found"
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

        public async Task<IEnumerable<CommentResponse>?> GetCommentsByTaskIdAsync(int taskId, int currentUserId, string role)
        {
            // First, check the task state
            var task = await _context.Tasks.FirstOrDefaultAsync(t => t.Id == taskId);

            // If task doesn't exist or is soft-deleted, return null (Controller will show "Task Not Found")
            if (task == null || task.IsDeleted) return null;

            bool isCreator = (role == "Admin" && task.CreatedBy == currentUserId);
            bool isAssigned = (task.AssignedToUserId == currentUserId);

            // If task exists but user has no access, throw specific error for the controller to catch
            if (!isCreator && !isAssigned) throw new UnauthorizedAccessException();

            var comments = await _context.Comments
                .Include(c => c.User)
                .Where(c => c.TaskId == taskId && !c.IsDeleted)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();

            return _mapper.Map<IEnumerable<CommentResponse>>(comments);
        }

        public async Task<CommentResponse?> UpdateCommentAsync(int commentId, CommentUpdateRequest request, int currentUserId)
        {
            // Check if comment exists AND the task it belongs to is not deleted
            var comment = await _context.Comments
                .Include(c => c.User)
                .Include(c => c.Task)
                .FirstOrDefaultAsync(c => c.Id == commentId && !c.IsDeleted);

            // If task is deleted, the comment is considered inaccessible
            if (comment == null || comment.Task == null || comment.Task.IsDeleted) return null;

            // Only the person who wrote the comment can edit it
            if (comment.UserId != currentUserId) return null;

            comment.Content = request.Content;
            await _context.SaveChangesAsync();
            return _mapper.Map<CommentResponse>(comment);
        }
    }
}