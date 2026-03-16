using AutoMapper;
using Microsoft.EntityFrameworkCore;
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

        public TaskService(TMDbContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }

        public async Task<TaskResponse?> GetTaskByIdAsync(int id, int currentUserId, string role)
        {
            var task = await _context.Tasks
                .Include(t => t.AssignedUser)
                .FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted);

            if (task == null) return null;

            if ((role.Equals("Admin", StringComparison.OrdinalIgnoreCase) && task.CreatedBy == currentUserId) ||
                 task.AssignedToUserId == currentUserId)
            {
                return _mapper.Map<TaskResponse>(task);
            }

            return null;
        }

        public async Task<TaskResponse> CreateTaskAsync(TaskCreateRequest request, int adminId)
        {
            
            if (request.AssignedToUserId != 0)
            {
                var targetUser = await _context.Users.FindAsync(request.AssignedToUserId);

                if (targetUser == null || targetUser.Role == UserRole.Admin || targetUser.Role == UserRole.SuperAdmin)
                {
                    throw new Exception("Tasks can only be assigned to regular Users, not Admins or SuperAdmins.");
                }
                if (targetUser.IsDeleted)
                {
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

            return _mapper.Map<TaskResponse>(taskWithDetails);
        }

        public async Task<bool> UpdateStatusAsync(int id, string statusName, int currentUserId, string role)
        {
            var task = await _context.Tasks.FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted);
            if (task == null) return false;

            bool isCreator = (role.Equals("Admin", StringComparison.OrdinalIgnoreCase) && task.CreatedBy == currentUserId);
            bool isAssigned = (task.AssignedToUserId == currentUserId);

            if (!isCreator && !isAssigned) return false;

            if (Enum.TryParse<TM.Model.Entities.TaskStatus>(statusName, true, out var parsedStatus))
            {
                task.Status = parsedStatus;
                await _context.SaveChangesAsync();
                return true;
            }
            return false;
        }

        public async Task<IEnumerable<TaskResponse>> GetAllTasksAsync(int userId, string role)
        {
            var query = _context.Tasks
                .Include(t => t.AssignedUser)
                .Where(t => !t.IsDeleted)
                .AsQueryable();

            if (role.Equals("Admin", StringComparison.OrdinalIgnoreCase))
                query = query.Where(t => t.CreatedBy == userId);
            else
                query = query.Where(t => t.AssignedToUserId == userId);

            var tasks = await query.ToListAsync();
            return _mapper.Map<IEnumerable<TaskResponse>>(tasks);
        }

        public async Task<TaskResponse?> UpdateTaskAsync(int id, TaskUpdateRequest request, int userId)
        {
            var task = await _context.Tasks.FirstOrDefaultAsync(t => t.Id == id);

            if (task == null || task.IsDeleted)
                throw new KeyNotFoundException("Task does not exist.");

            if (task.CreatedBy != userId)
                throw new UnauthorizedAccessException("Only the task creator can update task details.");

            if (task.Status == TM.Model.Entities.TaskStatus.Completed)
                throw new InvalidOperationException("This task is marked as Completed and cannot be edited.");

            task.Title = request.Title;
            task.Description = request.Description;
            task.AssignedToUserId = (request.AssignedToUserId == 0) ? null : request.AssignedToUserId;

            await _context.Entry(task).Reference(t => t.AssignedUser).LoadAsync();
            await _context.SaveChangesAsync();

            return _mapper.Map<TaskResponse>(task);
        }

        public async Task<bool?> DeleteTaskAsync(int id, int userId)
        {
            var task = await _context.Tasks
                .Include(t => t.Comments)
                .FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted);

            if (task == null) return null; 

            if (task.CreatedBy != userId) return false; 

            task.IsDeleted = true;
            if (task.Comments != null)
            {
                foreach (var comment in task.Comments)
                {
                    comment.IsDeleted = true;
                }
            }

            await _context.SaveChangesAsync();
            return true;
        }
    }
}