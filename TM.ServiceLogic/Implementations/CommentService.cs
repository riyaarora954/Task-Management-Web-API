using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
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
        private readonly ILogger<CommentService> _logger;

        public CommentService(TMDbContext context, IMapper mapper, ILogger<CommentService> logger)
        {
            _context = context;
            _mapper = mapper;
            _logger = logger;
        }

        // Admins can comment on their own tasks, and assigned users can comment on tasks assigned to them.
        public async Task<CommentResponse?> AddCommentAsync(CommentRequest request, int currentUserId, string role)
        {
            var sw = Stopwatch.StartNew();
            _logger.LogInformation("[CommentService] AddCommentAsync | UserId={UserId} Role={Role} TaskId={TaskId}", currentUserId, role, request.TaskId);

            var task = await _context.Tasks.FirstOrDefaultAsync(t => t.Id == request.TaskId && !t.IsDeleted);
            if (task == null)
            {
                sw.Stop();
                _logger.LogWarning("[CommentService] AddCommentAsync | TaskId={TaskId} NOT FOUND or deleted | {Elapsed}ms", request.TaskId, sw.ElapsedMilliseconds);
                return null;
            }

            bool isCreator = (role == "Admin" && task.CreatedBy == currentUserId);
            bool isAssigned = (task.AssignedToUserId == currentUserId);

            if (!isCreator && !isAssigned)
            {
                sw.Stop();
                _logger.LogWarning("[CommentService] AddCommentAsync | UNAUTHORIZED UserId={UserId} has no access to TaskId={TaskId} | {Elapsed}ms", currentUserId, request.TaskId, sw.ElapsedMilliseconds);
                return null;
            }

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

            sw.Stop();

            if (sw.ElapsedMilliseconds > 500)
                _logger.LogWarning("[CommentService] AddCommentAsync | SLOW — CommentId={CommentId} TaskId={TaskId} | {Elapsed}ms", comment.Id, request.TaskId, sw.ElapsedMilliseconds);
            else
                _logger.LogInformation("[CommentService] AddCommentAsync | SUCCESS CommentId={CommentId} TaskId={TaskId} | {Elapsed}ms", comment.Id, request.TaskId, sw.ElapsedMilliseconds);

            return _mapper.Map<CommentResponse>(savedComment);
        }

        // Admins can delete their own comments, and assigned users can delete their own comments on tasks assigned to them.
        public async Task<bool?> DeleteCommentAsync(int commentId, int currentUserId, string role)
        {
            var sw = Stopwatch.StartNew();
            _logger.LogInformation("[CommentService] DeleteCommentAsync | CommentId={CommentId} UserId={UserId} Role={Role}", commentId, currentUserId, role);

            var comment = await _context.Comments.Include(c => c.Task).FirstOrDefaultAsync(c => c.Id == commentId && !c.IsDeleted);

            if (comment == null || comment.Task == null || comment.Task.IsDeleted)
            {
                sw.Stop();
                _logger.LogWarning("[CommentService] DeleteCommentAsync | CommentId={CommentId} NOT FOUND or task deleted | {Elapsed}ms", commentId, sw.ElapsedMilliseconds);
                return null;
            }

            bool isAuthor = (comment.UserId == currentUserId);
            bool isTaskCreatorAdmin = (role == "Admin" && comment.Task?.CreatedBy == currentUserId);

            if (isAuthor || isTaskCreatorAdmin)
            {
                comment.IsDeleted = true;
                await _context.SaveChangesAsync();

                sw.Stop();
                _logger.LogInformation("[CommentService] DeleteCommentAsync | SUCCESS CommentId={CommentId} | {Elapsed}ms", commentId, sw.ElapsedMilliseconds);
                return true;
            }

            sw.Stop();
            _logger.LogWarning("[CommentService] DeleteCommentAsync | UNAUTHORIZED UserId={UserId} cannot delete CommentId={CommentId} | {Elapsed}ms", currentUserId, commentId, sw.ElapsedMilliseconds);
            return false;
        }

        // Admins can view comments on their own tasks, and assigned users can view comments on tasks assigned to them.
        public async Task<IEnumerable<CommentResponse>?> GetCommentsByTaskIdAsync(int taskId, int currentUserId, string role)
        {
            var sw = Stopwatch.StartNew();
            _logger.LogInformation("[CommentService] GetCommentsByTaskIdAsync | TaskId={TaskId} UserId={UserId} Role={Role}", taskId, currentUserId, role);

            var task = await _context.Tasks.FirstOrDefaultAsync(t => t.Id == taskId);
            if (task == null || task.IsDeleted)
            {
                sw.Stop();
                _logger.LogWarning("[CommentService] GetCommentsByTaskIdAsync | TaskId={TaskId} NOT FOUND or deleted | {Elapsed}ms", taskId, sw.ElapsedMilliseconds);
                return null;
            }

            bool isCreator = (role == "Admin" && task.CreatedBy == currentUserId);
            bool isAssigned = (task.AssignedToUserId == currentUserId);

            if (!isCreator && !isAssigned)
            {
                sw.Stop();
                _logger.LogWarning("[CommentService] GetCommentsByTaskIdAsync | UNAUTHORIZED UserId={UserId} for TaskId={TaskId} | {Elapsed}ms", currentUserId, taskId, sw.ElapsedMilliseconds);
                throw new UnauthorizedAccessException();
            }

            var comments = await _context.Comments
                .Include(c => c.User)
                .Where(c => c.TaskId == taskId && !c.IsDeleted)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();

            sw.Stop();

            if (sw.ElapsedMilliseconds > 500)
                _logger.LogWarning("[CommentService] GetCommentsByTaskIdAsync | SLOW TaskId={TaskId} | Count={Count} | {Elapsed}ms", taskId, comments.Count, sw.ElapsedMilliseconds);
            else
                _logger.LogInformation("[CommentService] GetCommentsByTaskIdAsync | TaskId={TaskId} | Count={Count} | {Elapsed}ms", taskId, comments.Count, sw.ElapsedMilliseconds);

            return _mapper.Map<IEnumerable<CommentResponse>>(comments);
        }

        // Admins can update their own comments, and assigned users can update their own comments on tasks assigned to them.
        public async Task<CommentResponse?> UpdateCommentAsync(int commentId, CommentUpdateRequest request, int currentUserId)
        {
            var sw = Stopwatch.StartNew();
            _logger.LogInformation("[CommentService] UpdateCommentAsync | CommentId={CommentId} UserId={UserId}", commentId, currentUserId);

            var comment = await _context.Comments
                .Include(c => c.User)
                .Include(c => c.Task)
                .FirstOrDefaultAsync(c => c.Id == commentId && !c.IsDeleted);

            if (comment == null || comment.Task == null || comment.Task.IsDeleted)
            {
                sw.Stop();
                _logger.LogWarning("[CommentService] UpdateCommentAsync | CommentId={CommentId} NOT FOUND or task deleted | {Elapsed}ms", commentId, sw.ElapsedMilliseconds);
                return null;
            }

            if (comment.UserId != currentUserId)
            {
                sw.Stop();
                _logger.LogWarning("[CommentService] UpdateCommentAsync | UNAUTHORIZED UserId={UserId} is not author of CommentId={CommentId} | {Elapsed}ms", currentUserId, commentId, sw.ElapsedMilliseconds);
                return null;
            }

            comment.Content = request.Content;
            await _context.SaveChangesAsync();

            sw.Stop();

            if (sw.ElapsedMilliseconds > 500)
                _logger.LogWarning("[CommentService] UpdateCommentAsync | SLOW CommentId={CommentId} | {Elapsed}ms", commentId, sw.ElapsedMilliseconds);
            else
                _logger.LogInformation("[CommentService] UpdateCommentAsync | SUCCESS CommentId={CommentId} | {Elapsed}ms", commentId, sw.ElapsedMilliseconds);

            return _mapper.Map<CommentResponse>(comment);
        }
    }
}