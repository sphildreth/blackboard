namespace Blackboard.Core.Models;

public class MessageBoard
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int RequiredLevel { get; set; } = 0;
    public bool IsModerated { get; set; } = false;
    public bool IsSticky { get; set; } = false;
    public bool IsHidden { get; set; } = false;
}

public class MessageThread
{
    public int Id { get; set; }
    public int BoardId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsLocked { get; set; } = false;
    public bool IsSticky { get; set; } = false;
}