using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using TM.Contracts.Tasks;
using TM.Model.Data;
using TM.Model.Entities;
using TM.ServiceLogic.Interfaces;

namespace TM.ServiceLogic.Implementations
{
    public class TaskService : ITaskService
    {
        private readonly TMDbContext _context;
        private readonly IMapper _mapper;
        private readonly ILogger<TaskService> _logger;

        public TaskService(TMDbContext context, IMapper mapper, ILogger<TaskService> logger)
        {
            _context = context;
            _mapper = mapper;
            _logger = logger;
        }

        public async Task<TaskResponse?> GetTaskByIdAsync(int id, int currentUserId, string role)
        {
            var sw = Stopwatch.StartNew();
            _logger.LogInformation("[TaskService] GetTaskByIdAsync | TaskId={TaskId} UserId={UserId} Role={Role}", id, currentUserId, role);

            var task = await _context.Tasks
                .Include(t => t.AssignedUser)
                .FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted);

            if (task == null)
            {
                sw.Stop();
                _logger.LogWarning("[TaskService] GetTaskByIdAsync | TaskId={TaskId} NOT FOUND or deleted | {Elapsed}ms", id, sw.ElapsedMilliseconds);
                return null;
            }

            bool hasAccess = (role.Equals("Admin", StringComparison.OrdinalIgnoreCase) && task.CreatedBy == currentUserId)
                             || task.AssignedToUserId == currentUserId;

            if (!hasAccess)
            {
                sw.Stop();
                _logger.LogWarning("[TaskService] GetTaskByIdAsync | UNAUTHORIZED UserId={UserId} has no access to TaskId={TaskId} | {Elapsed}ms", currentUserId, id, sw.ElapsedMilliseconds);
                return null;
            }

            sw.Stop();

            if (sw.ElapsedMilliseconds > 500)
                _logger.LogWarning("[TaskService] GetTaskByIdAsync | SLOW TaskId={TaskId} | {Elapsed}ms", id, sw.ElapsedMilliseconds);
            else
                _logger.LogInformation("[TaskService] GetTaskByIdAsync | SUCCESS TaskId={TaskId} | {Elapsed}ms", id, sw.ElapsedMilliseconds);

            return _mapper.Map<TaskResponse>(task);
        }

        public async Task<TaskResponse> CreateTaskAsync(TaskCreateRequest request, int adminId)
        {
            var sw = Stopwatch.StartNew();
            _logger.LogInformation("[TaskService] CreateTaskAsync | AdminId={AdminId} AssignedTo={AssignedTo}", adminId, request.AssignedToUserId);

            if (request.AssignedToUserId != 0)
            {
                var targetUser = await _context.Users.FindAsync(request.AssignedToUserId);

                if (targetUser == null || targetUser.Role == UserRole.Admin || targetUser.Role == UserRole.SuperAdmin)
                {
                    sw.Stop();
                    _logger.LogWarning("[TaskService] CreateTaskAsync | INVALID assignment target UserId={UserId} | {Elapsed}ms", request.AssignedToUserId, sw.ElapsedMilliseconds);
                    throw new Exception("Tasks can only be assigned to regular Users, not Admins or SuperAdmins.");
                }

                if (targetUser.IsDeleted)
                {
                    sw.Stop();
                    _logger.LogWarning("[TaskService] CreateTaskAsync | DELETED user UserId={UserId} cannot be assigned | {Elapsed}ms", request.AssignedToUserId, sw.ElapsedMilliseconds);
                    throw new Exception("User doesn't exists.");
                }
            }

            var task = _mapper.Map<TM.Model.Entities.Task>(request);
            task.AssignedToUserId = (request.AssignedToUserId == 0) ? null : request.AssignedToUserId;
            task.Status = TM.Model.Entities.TaskStatus.Pending;
            task.CreatedBy = adminId;
            task.IsDeleted = false;

            _context.Tasks.Add(task);
            await _context.SaveChangesAsync();

            var taskWithDetails = await _context.Tasks
                .Include(t => t.AssignedUser)
                .FirstOrDefaultAsync(t => t.Id == task.Id);

            sw.Stop();

            if (sw.ElapsedMilliseconds > 500)
                _logger.LogWarning("[TaskService] CreateTaskAsync | SLOW TaskId={TaskId} AdminId={AdminId} | {Elapsed}ms", task.Id, adminId, sw.ElapsedMilliseconds);
            else
                _logger.LogInformation("[TaskService] CreateTaskAsync | SUCCESS TaskId={TaskId} AdminId={AdminId} | {Elapsed}ms", task.Id, adminId, sw.ElapsedMilliseconds);

            return _mapper.Map<TaskResponse>(taskWithDetails);
        }

        public async Task<bool> UpdateStatusAsync(int id, string statusName, int currentUserId, string role)
        {
            var sw = Stopwatch.StartNew();
            _logger.LogInformation("[TaskService] UpdateStatusAsync | TaskId={TaskId} NewStatus={Status} UserId={UserId} Role={Role}", id, statusName, currentUserId, role);

            var task = await _context.Tasks.FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted);
            if (task == null)
            {
                sw.Stop();
                _logger.LogWarning("[TaskService] UpdateStatusAsync | TaskId={TaskId} NOT FOUND or deleted | {Elapsed}ms", id, sw.ElapsedMilliseconds);
                return false;
            }

            bool isCreator = (role.Equals("Admin", StringComparison.OrdinalIgnoreCase) && task.CreatedBy == currentUserId);
            bool isAssigned = (task.AssignedToUserId == currentUserId);

            if (!isCreator && !isAssigned)
            {
                sw.Stop();
                _logger.LogWarning("[TaskService] UpdateStatusAsync | UNAUTHORIZED UserId={UserId} for TaskId={TaskId} | {Elapsed}ms", currentUserId, id, sw.ElapsedMilliseconds);
                return false;
            }

            if (Enum.TryParse<TM.Model.Entities.TaskStatus>(statusName, true, out var parsedStatus))
            {
                var previousStatus = task.Status;
                task.Status = parsedStatus;
                await _context.SaveChangesAsync();

                sw.Stop();
                _logger.LogInformation("[TaskService] UpdateStatusAsync | SUCCESS TaskId={TaskId} {OldStatus}→{NewStatus} | {Elapsed}ms", id, previousStatus, parsedStatus, sw.ElapsedMilliseconds);
                return true;
            }

            sw.Stop();
            _logger.LogWarning("[TaskService] UpdateStatusAsync | INVALID status value='{Status}' for TaskId={TaskId} | {Elapsed}ms", statusName, id, sw.ElapsedMilliseconds);
            return false;
        }

        public async Task<IEnumerable<TaskResponse>> GetAllTasksAsync(int userId, string role)
        {
            var sw = Stopwatch.StartNew();
            _logger.LogInformation("[TaskService] GetAllTasksAsync | UserId={UserId} Role={Role}", userId, role);

            var query = _context.Tasks
                .Include(t => t.AssignedUser)
                .Where(t => !t.IsDeleted)
                .AsQueryable();

            if (role.Equals("Admin", StringComparison.OrdinalIgnoreCase))
                query = query.Where(t => t.CreatedBy == userId);
            else
                query = query.Where(t => t.AssignedToUserId == userId);

            var tasks = await query.ToListAsync();

            sw.Stop();

            if (sw.ElapsedMilliseconds > 500)
                _logger.LogWarning("[TaskService] GetAllTasksAsync | SLOW UserId={UserId} Role={Role} | Count={Count} | {Elapsed}ms", userId, role, tasks.Count, sw.ElapsedMilliseconds);
            else
                _logger.LogInformation("[TaskService] GetAllTasksAsync | UserId={UserId} Role={Role} | Count={Count} | {Elapsed}ms", userId, role, tasks.Count, sw.ElapsedMilliseconds);

            return _mapper.Map<IEnumerable<TaskResponse>>(tasks);
        }

        public async Task<TaskResponse?> UpdateTaskAsync(int id, TaskUpdateRequest request, int userId)
        {
            var sw = Stopwatch.StartNew();
            _logger.LogInformation("[TaskService] UpdateTaskAsync | TaskId={TaskId} UserId={UserId}", id, userId);

            var task = await _context.Tasks.FirstOrDefaultAsync(t => t.Id == id);

            if (task == null || task.IsDeleted)
            {
                sw.Stop();
                _logger.LogWarning("[TaskService] UpdateTaskAsync | TaskId={TaskId} NOT FOUND or deleted | {Elapsed}ms", id, sw.ElapsedMilliseconds);
                throw new KeyNotFoundException("Task does not exist.");
            }

            if (task.CreatedBy != userId)
            {
                sw.Stop();
                _logger.LogWarning("[TaskService] UpdateTaskAsync | UNAUTHORIZED UserId={UserId} is not creator of TaskId={TaskId} | {Elapsed}ms", userId, id, sw.ElapsedMilliseconds);
                throw new UnauthorizedAccessException("Only the task creator can update task details.");
            }

            if (task.Status == TM.Model.Entities.TaskStatus.Completed)
            {
                sw.Stop();
                _logger.LogWarning("[TaskService] UpdateTaskAsync | BLOCKED TaskId={TaskId} is Completed and cannot be edited | {Elapsed}ms", id, sw.ElapsedMilliseconds);
                throw new InvalidOperationException("This task is marked as Completed and cannot be edited.");
            }

            task.Title = request.Title;
            task.Description = request.Description;
            task.AssignedToUserId = (request.AssignedToUserId == 0) ? null : request.AssignedToUserId;

            await _context.Entry(task).Reference(t => t.AssignedUser).LoadAsync();
            await _context.SaveChangesAsync();

            sw.Stop();

            if (sw.ElapsedMilliseconds > 500)
                _logger.LogWarning("[TaskService] UpdateTaskAsync | SLOW TaskId={TaskId} | {Elapsed}ms", id, sw.ElapsedMilliseconds);
            else
                _logger.LogInformation("[TaskService] UpdateTaskAsync | SUCCESS TaskId={TaskId} | {Elapsed}ms", id, sw.ElapsedMilliseconds);

            return _mapper.Map<TaskResponse>(task);
        }

        public async Task<bool?> DeleteTaskAsync(int id, int userId)
        {
            var sw = Stopwatch.StartNew();
            _logger.LogInformation("[TaskService] DeleteTaskAsync | TaskId={TaskId} UserId={UserId}", id, userId);

            var task = await _context.Tasks
                .Include(t => t.Comments)
                .FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted);

            if (task == null)
            {
                sw.Stop();
                _logger.LogWarning("[TaskService] DeleteTaskAsync | TaskId={TaskId} NOT FOUND or already deleted | {Elapsed}ms", id, sw.ElapsedMilliseconds);
                return null;
            }

            if (task.CreatedBy != userId)
            {
                sw.Stop();
                _logger.LogWarning("[TaskService] DeleteTaskAsync | UNAUTHORIZED UserId={UserId} is not creator of TaskId={TaskId} | {Elapsed}ms", userId, id, sw.ElapsedMilliseconds);
                return false;
            }

            task.IsDeleted = true;

            int commentCount = 0;
            if (task.Comments != null)
            {
                foreach (var comment in task.Comments)
                    comment.IsDeleted = true;
                commentCount = task.Comments.Count;
            }

            await _context.SaveChangesAsync();

            sw.Stop();

            if (sw.ElapsedMilliseconds > 500)
                _logger.LogWarning("[TaskService] DeleteTaskAsync | SLOW TaskId={TaskId} CommentsDeleted={CommentCount} | {Elapsed}ms", id, commentCount, sw.ElapsedMilliseconds);
            else
                _logger.LogInformation("[TaskService] DeleteTaskAsync | SUCCESS TaskId={TaskId} CommentsDeleted={CommentCount} | {Elapsed}ms", id, commentCount, sw.ElapsedMilliseconds);

            return true;
        }
    }
}