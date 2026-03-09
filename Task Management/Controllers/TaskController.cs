using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TM.Contracts.Tasks;
using TM.ServiceLogic.Interfaces;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class TasksController : ControllerBase
{
    private readonly ITaskService _taskService;

    public TasksController(ITaskService taskService) => _taskService = taskService;

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var task = await _taskService.GetTaskByIdAsync(id);
        return task != null ? Ok(task) : NotFound();
    }

    [Authorize(Roles = "Admin")] 
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] TaskCreateRequest request)
    {
        var result = await _taskService.CreateTaskAsync(request);
        return Ok(result);
    }

    [HttpPatch("{id}/status")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] TaskStatusRequest request)
    {
        
        var success = await _taskService.UpdateStatusAsync(id, request.Status);

        if (!success) return BadRequest("Invalid status name provided.");

        return NoContent();
    }
}