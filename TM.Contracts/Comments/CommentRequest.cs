using System;
namespace TM.Contracts.Comments
{
    public class CommentRequest
    {
        public string Content { get; set; } = string.Empty;
        public int TaskId { get; set; }
    }
}