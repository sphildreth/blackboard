using System.ComponentModel.DataAnnotations;

namespace Blackboard.Core.Models;

public class FileRating
{
    public int Id { get; set; }

    public int FileId { get; set; }
    public int UserId { get; set; }

    [Range(1, 5)]
    public int Rating { get; set; }

    [StringLength(1000)]
    public string? Comment { get; set; }

    public DateTime RatingDate { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual BbsFile File { get; set; } = null!;
    public virtual User User { get; set; } = null!;
}
