using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
namespace TM.Model.Entities
{

    public class Comment : BaseEntity
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Content { get; set; } = string.Empty;

        public Guid TaskId { get; set; }
        public Task Task { get; set; } = null!;

        public Guid UserId { get; set; }
        public User User { get; set; } = null!;
    }
}
