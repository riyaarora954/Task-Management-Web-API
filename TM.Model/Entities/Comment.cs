using System.ComponentModel.DataAnnotations.Schema;

namespace TM.Model.Entities
{
    public class Comment : BaseEntity
    {
        public string Content { get; set; } = string.Empty;
        public int TaskId { get; set; }

        [ForeignKey("TaskId")]
        public Task? Task { get; set; }

        public int UserId { get; set; }

        [ForeignKey("UserId")]
        public User? User { get; set; }
        public bool IsDeleted { get; set; } = false;
    }
}