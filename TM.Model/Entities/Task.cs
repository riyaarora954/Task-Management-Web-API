using System.Collections.Generic;

namespace TM.Model.Entities
{
    public class Task
    {
        // Primary Key for the Database
        public int Id { get; set; }

        public string Title { get; set; }

        public string Description { get; set; }

        // Using the Enum we just created above
        public TaskStatus Status { get; set; }

        // This links the task to a specific User
        public int AssignedToUserId { get; set; }

        // The Link: One task can have many comments. 
        // We initialize it to an empty list so it's never "null" and doesn't crash.
        public List<Comment> Comments { get; set; } = new List<Comment>();
    }
}