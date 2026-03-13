using TM.Contracts.Tasks;

namespace TM.ServiceLogic.Interfaces
{
    public interface ITaskService
    {
        // Update the signature to accept the ID and Role
        Task<TaskResponse?> GetTaskByIdAsync(int id, int currentUserId, string role);

        Task<TaskResponse> CreateTaskAsync(TaskCreateRequest request, int adminId);

        // Update the signature to include security parameters
        Task<bool> UpdateStatusAsync(int id, string statusName, int currentUserId, string role);


        Task<IEnumerable<TaskResponse>> GetAllTasksAsync(int userId, string role);
        Task<TaskResponse?> UpdateTaskAsync(int id, TaskUpdateRequest request, int userId);
        Task<bool?> DeleteTaskAsync(int id, int userId);
    }
}