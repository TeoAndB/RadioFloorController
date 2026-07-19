namespace RadioFloorController.Data;

/// <summary>
/// EF Core persistence entity for "which user currently holds the floor for a given radio group".
/// This is a mutable EF entity by necessity (change tracking) — do not use it as a domain model;
/// map to/from an immutable domain type at the repository boundary instead.
/// </summary>
public class FloorGrantEntity
{
    /// <summary>Radio group identifier. Primary key.</summary>
    public string GroupId { get; set; } = string.Empty;

    /// <summary>User id currently holding the floor for this group; null means the floor is free.</summary>
    public string? HolderUserId { get; set; }

    /// <summary>Timestamp the current holder obtained the floor; null when the floor is free.</summary>
    public DateTimeOffset? ObtainedAt { get; set; }

    /// <summary>Deadline at which the current hold auto-expires (obtained-at + configured timeout duration); null when the floor is free.</summary>
    public DateTimeOffset? ExpiresAt { get; set; }

    /// <summary>Free-text reason for the most recent release of this group's floor (e.g. "Manual" or "TimedOut", written by the service layer); null if never released.</summary>
    public string? LastReleaseReason { get; set; }

    /// <summary>Timestamp of the most recent release (manual or timeout); null if never released.</summary>
    public DateTimeOffset? LastReleasedAt { get; set; }

    /// <summary>User id of the most recent holder, retained after release so callers can see who last held the floor even after it's free; null if never held.</summary>
    public string? LastHolderUserId { get; set; }
}
