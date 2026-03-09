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
}