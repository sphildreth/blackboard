using System.ComponentModel.DataAnnotations;

namespace Blackboard.Core.Models;

public class BbsFile
{
    public int Id { get; set; }

    public int AreaId { get; set; }

    [Required]
    [StringLength(255)]
    public string FileName { get; set; } = string.Empty;

    [Required]
    [StringLength(255)]
    public string OriginalFileName { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    [Required]
    [StringLength(500)]
    public string FilePath { get; set; } = string.Empty;

    public long Size { get; set; }

    [Required]
    [StringLength(64)]
    public string Checksum { get; set; } = string.Empty;

    [StringLength(100)]
    public string? MimeType { get; set; }

    public string? Tags { get; set; } // JSON array of tags

    public DateTime UploadDate { get; set; } = DateTime.UtcNow;
    public int? UploaderId { get; set; }
    public int DownloadCount { get; set; } = 0;
    public DateTime? LastDownloadAt { get; set; }
    public bool IsApproved { get; set; } = false;
    public int? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? ExpiresAt { get; set; }

    // Navigation properties
    public virtual FileArea Area { get; set; } = null!;
    public virtual User? Uploader { get; set; }
    public virtual User? Approver { get; set; }
    public virtual ICollection<FileRating> Ratings { get; set; } = new List<FileRating>();

    // Helper properties
    public string[] TagsArray 
    { 
        get => string.IsNullOrEmpty(Tags) ? Array.Empty<string>() : 
               System.Text.Json.JsonSerializer.Deserialize<string[]>(Tags) ?? Array.Empty<string>();
        set => Tags = value.Length > 0 ? System.Text.Json.JsonSerializer.Serialize(value) : null;
    }

    public double AverageRating 
    { 
        get => Ratings.Any() ? Ratings.Average(r => r.Rating) : 0.0;
    }

    public string SizeFormatted 
    { 
        get 
        {
            if (Size < 1024) return $"{Size} B";
            if (Size < 1048576) return $"{Size / 1024.0:F1} KB";
            if (Size < 1073741824) return $"{Size / 1048576.0:F1} MB";
            return $"{Size / 1073741824.0:F1} GB";
        }
    }
}
