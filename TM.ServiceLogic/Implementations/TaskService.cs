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


    public async Task<TaskResponse?> GetTaskByIdAsync(int id)
    {
        var task = await _context.Tasks
            .Include(t => t.AssignedUser) 
            .FirstOrDefaultAsync(t => t.Id == id);

        return _mapper.Map<TaskResponse>(task);
    }

    // 2. POST (Create)
    public async Task<TaskResponse> CreateTaskAsync(TaskCreateRequest request)
    {
        var task = _mapper.Map<TM.Model.Entities.Task>(request);
        task.Status = TM.Model.Entities.TaskStatus.Pending;

        _context.Tasks.Add(task);
        await _context.SaveChangesAsync();


        var taskWithUser = await _context.Tasks
            .Include(t => t.AssignedUser) 
            .FirstOrDefaultAsync(t => t.Id == task.Id);

        return _mapper.Map<TaskResponse>(taskWithUser);
    }

    public async Task<bool> UpdateStatusAsync(int id, string statusName)
    {
        var task = await _context.Tasks.FindAsync(id);
        if (task == null) return false;


        if (Enum.TryParse<TM.Model.Entities.TaskStatus>(statusName, true, out var parsedStatus))
        {
            task.Status = parsedStatus;
            await _context.SaveChangesAsync();
            return true;
        }

        return false; 
    }



    // --- NEW METHODS ---

    // 1. GET ALL
    public async Task<IEnumerable<TaskResponse>> GetAllTasksAsync()
    {
        // Fetch all tasks and include the assigned user for mapping
        var tasks = await _context.Tasks
            .Include(t => t.AssignedUser)
            .ToListAsync();

        return _mapper.Map<IEnumerable<TaskResponse>>(tasks);
    }

    // 2. UPDATE TASK
    public async Task<TaskResponse?> UpdateTaskAsync(int id, TaskUpdateRequest request)
    {
        // Find the task we want to update
        var task = await _context.Tasks.FirstOrDefaultAsync(t => t.Id == id);

        if (task == null) return null; // Let the controller know it wasn't found

        // Update the properties
        task.Title = request.Title;
        task.Description = request.Description;
        task.AssignedToUserId = request.AssignedToUserId;

        await _context.SaveChangesAsync();

        // Reload the AssignedUser reference so AutoMapper grabs the new username if it changed
        await _context.Entry(task).Reference(t => t.AssignedUser).LoadAsync();

        return _mapper.Map<TaskResponse>(task);
    }

    // 3. DELETE TASK
    public async Task<bool> DeleteTaskAsync(int id)
    {
        var task = await _context.Tasks.FindAsync(id);

        if (task == null) return false;

        _context.Tasks.Remove(task);
        await _context.SaveChangesAsync();

        return true;
    }
}


