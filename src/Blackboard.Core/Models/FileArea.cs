using System.ComponentModel.DataAnnotations;

namespace Blackboard.Core.Models;

public class FileArea
{
    public int Id { get; set; }

    [Required] [StringLength(100)] public string Name { get; set; } = string.Empty;

    [StringLength(500)] public string? Description { get; set; }

    [Required] [StringLength(500)] public string Path { get; set; } = string.Empty;

    public int RequiredLevel { get; set; } = 0;
    public int UploadLevel { get; set; } = 10;
    public bool IsActive { get; set; } = true;
    public long MaxFileSize { get; set; } = 10485760; // 10MB default
    public bool AllowUploads { get; set; } = true;
    public bool AllowDownloads { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual ICollection<BbsFile> Files { get; set; } = new List<BbsFile>();
}