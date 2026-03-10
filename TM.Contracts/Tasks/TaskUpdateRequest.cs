using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TM.Contracts.Tasks
{
    public class TaskUpdateRequest
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        // Using int? allows them to unassign a task by passing null
        public int? AssignedToUserId { get; set; }
    }
}