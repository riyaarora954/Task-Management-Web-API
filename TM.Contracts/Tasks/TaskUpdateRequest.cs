namespace TM.Contracts.Tasks
{
    public class TaskUpdateRequest
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        public int? AssignedToUserId { get; set; }

        public DateTime? DueDate { get; set; }
    }
}