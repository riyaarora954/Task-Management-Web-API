using TM.Contracts.Tasks;

namespace TM.ServiceLogic.Interfaces
{
    public interface ITaskService
    {
        Task<TaskResponse?> GetTaskByIdAsync(int id, int currentUserId, string role);

        Task<TaskResponse> CreateTaskAsync(TaskCreateRequest request, int adminId);
        Task<bool> UpdateStatusAsync(int id, string statusName, int currentUserId, string role);


        Task<IEnumerable<TaskResponse>> GetAllTasksAsync(int userId, string role);
        Task<TaskResponse?> UpdateTaskAsync(int id, TaskUpdateRequest request, int userId);
        Task<bool?> DeleteTaskAsync(int id, int userId);
    }
}