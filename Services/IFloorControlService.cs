using RadioFloorController.Domain;

namespace RadioFloorController.Services;

/// <summary>
/// Floor-control mutual-exclusion service: at most one user may hold the floor for a
/// given radio group at any time. A hold that isn't manually released within the
/// configured <see cref="FloorControlOptions.HoldTimeout"/> is treated as expired and
/// becomes claimable again — see <see cref="ObtainFloorAsync"/> and
/// <see cref="Services.FloorTimeoutSweepService"/>.
/// </summary>
public interface IFloorControlService
{
    /// <summary>
    /// Attempts to obtain the floor for <paramref name="groupId"/> on behalf of
    /// <paramref name="userId"/>. Succeeds when the floor is free, already held by the same
    /// user, or held by a different user whose hold has passed its expiry deadline (an
    /// expired hold is treated as free for claiming purposes); fails with
    /// <see cref="FloorObtainResult.Conflict"/> when a different user holds it and that hold
    /// has not yet expired.
    /// </summary>
    Task<FloorObtainResult> ObtainFloorAsync(string groupId, string userId, CancellationToken ct = default);

    /// <summary>
    /// Attempts to release the floor for <paramref name="groupId"/> on behalf of
    /// <paramref name="userId"/>. Succeeds only when that user is the current holder;
    /// distinguishes <see cref="FloorReleaseResult.TimedOut"/> (the caller's own hold already
    /// expired before this call) from <see cref="FloorReleaseResult.NotHolder"/> (the caller
    /// never held it, or a different user currently does).
    /// </summary>
    Task<FloorReleaseResult> ReleaseFloorAsync(string groupId, string userId, CancellationToken ct = default);

    /// <summary>
    /// Reads who (if anyone) currently, validly holds the floor for <paramref name="groupId"/>.
    /// Returns <see cref="FloorHolderResult.NotHeld"/> if nobody does — whether because the
    /// group has no history, the floor was manually released, or the most recent hold already
    /// expired. Reflects an already-expired hold as not-held even if the background sweep has
    /// not yet physically updated the row.
    /// </summary>
    Task<FloorHolderResult> GetCurrentHolderAsync(string groupId, CancellationToken ct = default);
}
