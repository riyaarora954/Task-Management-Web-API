using TM.Contracts.Tasks;

namespace TM.ServiceLogic.Interfaces
{
    public interface ITaskService
    {
        Task<TaskResponse?> GetTaskByIdAsync(int id);

        Task<TaskResponse> CreateTaskAsync(TaskCreateRequest request);

        Task<bool> UpdateStatusAsync(int id, string statusName);


        Task<IEnumerable<TaskResponse>> GetAllTasksAsync();
        Task<TaskResponse?> UpdateTaskAsync(int id, TaskUpdateRequest request);
        Task<bool> DeleteTaskAsync(int id);
    }
}