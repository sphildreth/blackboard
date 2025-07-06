using System.ComponentModel.DataAnnotations;

namespace Blackboard.Core.Models;

public class Door
{
    public int Id { get; set; }

    [Required] [StringLength(100)] public string Name { get; set; } = string.Empty;

    [StringLength(500)] public string? Description { get; set; }

    [Required] [StringLength(50)] public string Category { get; set; } = string.Empty;

    [Required] [StringLength(500)] public string ExecutablePath { get; set; } = string.Empty;

    [StringLength(1000)] public string? CommandLine { get; set; }

    [StringLength(500)] public string? WorkingDirectory { get; set; }

    [Required] [StringLength(20)] public string DropFileType { get; set; } = "DOOR.SYS";

    [StringLength(500)] public string? DropFileLocation { get; set; }

    public bool IsActive { get; set; } = true;
    public bool RequiresDosBox { get; set; } = false;
    public string? DosBoxConfigPath { get; set; }
    public string? SerialPort { get; set; } = "COM1";
    public int MemorySize { get; set; } = 16; // MB

    // Access Control
    public int MinimumLevel { get; set; } = 0;
    public int MaximumLevel { get; set; } = 255;
    public int TimeLimit { get; set; } = 60; // minutes
    public int DailyLimit { get; set; } = 5; // sessions per day
    public int Cost { get; set; } = 0; // credits required

    // Scheduling
    public bool SchedulingEnabled { get; set; } = false;
    public string? AvailableHours { get; set; } // "06:00-23:00"
    public string? TimeZone { get; set; } = "UTC";

    // Multi-node support
    public bool MultiNodeEnabled { get; set; } = false;
    public int MaxPlayers { get; set; } = 1;
    public bool InterBbsEnabled { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedBy { get; set; }

    // Navigation properties
    public virtual ICollection<DoorSession> Sessions { get; set; } = new List<DoorSession>();
    public virtual ICollection<DoorConfig> Configs { get; set; } = new List<DoorConfig>();
    public virtual ICollection<DoorPermission> Permissions { get; set; } = new List<DoorPermission>();
}

public class DoorConfig
{
    public int Id { get; set; }
    public int DoorId { get; set; }

    [Required] [StringLength(100)] public string ConfigKey { get; set; } = string.Empty;

    public string? ConfigValue { get; set; }

    [Required] [StringLength(20)] public string ConfigType { get; set; } = "string"; // string, int, bool, path

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual Door Door { get; set; } = null!;
}

public class DoorPermission
{
    public int Id { get; set; }
    public int DoorId { get; set; }
    public int? UserId { get; set; } // null = applies to all users
    public string? UserGroup { get; set; }

    [Required] [StringLength(20)] public string AccessType { get; set; } = "allow"; // allow, deny

    public DateTime GrantedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; }
    public int? GrantedBy { get; set; }

    // Navigation properties
    public virtual Door Door { get; set; } = null!;
}

public class DoorSession
{
    public int Id { get; set; }
    public string SessionId { get; set; } = Guid.NewGuid().ToString();
    public int DoorId { get; set; }
    public int UserId { get; set; }
    public int? NodeNumber { get; set; }

    public DateTime StartTime { get; set; } = DateTime.UtcNow;
    public DateTime? EndTime { get; set; }
    public int Duration => EndTime.HasValue ? (int)(EndTime.Value - StartTime).TotalSeconds : 0;

    public int? ExitCode { get; set; }
    public string? DropFilePath { get; set; }
    public string? WorkingDirectory { get; set; }
    public int? ProcessId { get; set; }

    [StringLength(20)] public string Status { get; set; } = "starting"; // starting, running, completed, failed, terminated

    public string? ErrorMessage { get; set; }
    public DateTime LastActivity { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual Door Door { get; set; } = null!;
}

public class DoorStatistics
{
    public int Id { get; set; }
    public int DoorId { get; set; }
    public int UserId { get; set; }

    public int TotalSessions { get; set; } = 0;
    public int TotalTime { get; set; } = 0; // seconds
    public DateTime? LastPlayed { get; set; }
    public int? HighScore { get; set; }
    public string? HighScoreData { get; set; } // JSON for complex score data

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual Door Door { get; set; } = null!;
}

public class DoorLog
{
    public int Id { get; set; }
    public int? SessionId { get; set; }
    public int DoorId { get; set; }

    [Required] [StringLength(20)] public string LogLevel { get; set; } = "info"; // debug, info, warning, error

    [Required] public string Message { get; set; } = string.Empty;

    public string? Details { get; set; } // JSON for additional data
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual DoorSession? Session { get; set; }
    public virtual Door Door { get; set; } = null!;
}