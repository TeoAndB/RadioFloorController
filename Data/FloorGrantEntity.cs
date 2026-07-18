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
}
