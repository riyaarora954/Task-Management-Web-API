using AutoMapper;
using Microsoft.EntityFrameworkCore;
using TM.Contracts.Tasks;
using TM.Model.Data;
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
            var task = await _context.Tasks.FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted);

            if (task == null || task.CreatedBy != userId) return null;

            task.Title = request.Title;
            task.Description = request.Description;

            task.AssignedToUserId = (request.AssignedToUserId == 0) ? null : request.AssignedToUserId;

            await _context.Entry(task).Reference(t => t.AssignedUser).LoadAsync();

            return _mapper.Map<TaskResponse>(task);
        }

        public async Task<bool?> DeleteTaskAsync(int id, int userId)
        {
            var task = await _context.Tasks.FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted);

            if (task == null || task.CreatedBy != userId) return null;

            if (task.Status != TM.Model.Entities.TaskStatus.Pending) return false;

            task.IsDeleted = true;
            await _context.SaveChangesAsync();

            return true;
        }
    }
}