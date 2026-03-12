using System;
namespace TM.Contracts.Comments
{
    public class CommentResponse
    {
        public int Id { get; set; }
        public string Content { get; set; } = string.Empty;
        public string AuthorName { get; set; } = string.Empty; 
        public DateTime CreatedAt { get; set; }
    }
}