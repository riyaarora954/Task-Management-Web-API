using TM.Contracts.Tasks;

namespace TM.ServiceLogic.Interfaces
{
    public interface ITaskService
    {
        Task<TaskResponse?> GetTaskByIdAsync(int id);

        Task<TaskResponse> CreateTaskAsync(TaskCreateRequest request);

        Task<bool> UpdateStatusAsync(int id, string statusName);
    }
}