using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace TM.Model.Entities
{
    public class Task : BaseEntity
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public TaskStatus Status { get; set; } = TaskStatus.Pending;

        public int? AssignedToUserId { get; set; }

        [ForeignKey("AssignedToUserId")]
        public User? AssignedUser { get; set; }

        public List<Comment> Comments { get; set; } = new();

        public int CreatedBy { get; set; }
    }
}