using AutoMapper;
using Microsoft.EntityFrameworkCore;
using TM.Contracts.Tasks;
using TM.Model.Data;
using TM.Model.Entities;
using TM.ServiceLogic.Interfaces;


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
            .FirstOrDefaultAsync(t => t.Id == id);

        if (task == null) return null;

        // 🛡️ THE MODIFICATION:
        // 1. If the user is an Admin AND they created the task
        // 2. OR if the user is the one assigned to the task
        if ((role == "Admin" && task.CreatedBy == currentUserId) || task.AssignedToUserId == currentUserId)
        {
            return _mapper.Map<TaskResponse>(task);
        }

        // If they don't match either, we return null
        return null;
    }

    // 2. POST (Create)
    // 1. Update the signature to accept creatorId
    public async Task<TaskResponse> CreateTaskAsync(TaskCreateRequest request, int adminId)
    {
        // Map the DTO to the Entity
        var task = _mapper.Map<TM.Model.Entities.Task>(request);

        // Set internal system fields automatically
        task.Status = TM.Model.Entities.TaskStatus.Pending;
        task.CreatedBy = adminId; // Stored from the token, not the request body

        _context.Tasks.Add(task);
        await _context.SaveChangesAsync();

        // Re-fetch to include user details for the response
        var taskWithDetails = await _context.Tasks
            .Include(t => t.AssignedUser)
            .FirstOrDefaultAsync(t => t.Id == task.Id);

        return _mapper.Map<TaskResponse>(taskWithDetails);
    }

    public async Task<bool> UpdateStatusAsync(int id, string statusName, int currentUserId, string role)
    {
        var task = await _context.Tasks.FindAsync(id);

        if (task == null) return false;

        // 🛡️ SECURITY CHECK:
        // Only the Creator (Admin) or the Assigned User can change the status
        bool isCreator = (role == "Admin" && task.CreatedBy == currentUserId);
        bool isAssigned = (task.AssignedToUserId == currentUserId);

        if (!isCreator && !isAssigned)
        {
            return false; // Security violation or not found
        }

        if (Enum.TryParse<TM.Model.Entities.TaskStatus>(statusName, true, out var parsedStatus))
        {
            task.Status = parsedStatus;
            await _context.SaveChangesAsync();
            return true;
        }

        return false;
    }



    // --- NEW UPDATED METHODS (Matching Teammate's Auth Style) ---

    // 1. GET ALL (User-Specific)
    public async Task<IEnumerable<TaskResponse>> GetAllTasksAsync(int userId, string role)
    {
        var query = _context.Tasks.Include(t => t.AssignedUser).AsQueryable();

        if (role == "Admin")
        {
            // Admins only see tasks THEY created (using CreatedBy from BaseEntity)
            query = query.Where(t => t.CreatedBy == userId);
        }
        else
        {
            // Regular Users only see tasks ASSIGNED to them
            query = query.Where(t => t.AssignedToUserId == userId);
        }

        var tasks = await query.ToListAsync();
        return _mapper.Map<IEnumerable<TaskResponse>>(tasks);
    }

    // 2. UPDATE TASK (Creator-Specific)
    public async Task<TaskResponse?> UpdateTaskAsync(int id, TaskUpdateRequest request, int userId)
    {
        var task = await _context.Tasks.FirstOrDefaultAsync(t => t.Id == id);

        // Matches Security Style: Only the Admin who created it can update it
        if (task == null || task.CreatedBy != userId)
        {
            return null;
        }

        task.Title = request.Title;
        task.Description = request.Description;
        task.AssignedToUserId = request.AssignedToUserId;

        await _context.SaveChangesAsync();

        // Reload user info for the response
        await _context.Entry(task).Reference(t => t.AssignedUser).LoadAsync();

        return _mapper.Map<TaskResponse>(task);
    }

    // 3. DELETE TASK (Creator-Specific)
    public async Task<bool> DeleteTaskAsync(int id, int userId)
    {
        var task = await _context.Tasks.FindAsync(id);

        // Matches Security Style: Only the Admin who created it can delete it
        if (task == null || task.CreatedBy != userId)
        {
            return false;
        }

        _context.Tasks.Remove(task);
        await _context.SaveChangesAsync();

        return true;
    }
}


