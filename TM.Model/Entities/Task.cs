using System.Collections.Generic;

namespace TM.Model.Entities
{
    public class Task : BaseEntity
    {
        public Guid Id { get; set; }

        public string Title { get; set; }

        public string Description { get; set; }

        public TaskStatus Status { get; set; }

        public int AssignedToUserId { get; set; }

        public List<Comment> Comments { get; set; } = new List<Comment>();
    }
}