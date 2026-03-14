namespace TM.Contracts.Comments
{
    public class CommentUpdateRequest
    {
        // We removed TaskId from here!
        public string Content { get; set; } = string.Empty;
    }
}