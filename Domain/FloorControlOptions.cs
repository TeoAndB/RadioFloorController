namespace RadioFloorController.Domain;

/// <summary>
/// Configuration for automatic floor-hold timeouts, bound from the <c>"FloorControl"</c>
/// configuration section. Both <see cref="TimeSpan"/> values bind from the standard
/// <c>"hh:mm:ss"</c> configuration string format, e.g. in <c>appsettings.json</c>:
/// <code>
/// "FloorControl": {
///   "HoldTimeout": "00:00:30",
///   "SweepInterval": "00:00:05"
/// }
/// </code>
/// Either or both keys may be omitted; unconfigured properties fall back to the defaults
/// documented below.
/// </summary>
public sealed class FloorControlOptions
{
    /// <summary>Configuration section name this type binds to.</summary>
    public const string SectionName = "FloorControl";

    /// <summary>
    /// How long a user may hold the floor before it is automatically eligible for release —
    /// either lazily (the next obtain attempt for that group reclaims it) or proactively (the
    /// background sweep frees it). Bound from <c>FloorControl:HoldTimeout</c>. Defaults to
    /// 120 seconds when unconfigured.
    /// </summary>
    public TimeSpan HoldTimeout { get; set; } = TimeSpan.FromSeconds(120);

    /// <summary>
    /// How often the background sweep (<see cref="Services.FloorTimeoutSweepService"/>) checks
    /// for and releases expired holds. Bound from <c>FloorControl:SweepInterval</c>. Defaults
    /// to 5 seconds when unconfigured.
    /// </summary>
    public TimeSpan SweepInterval { get; set; } = TimeSpan.FromSeconds(5);
}
