using RadioFloorController.Domain;

namespace RadioFloorController.Services;

/// <summary>
/// Floor-control mutual-exclusion service: at most one user may hold the floor for a
/// given radio group at any time.
/// </summary>
public interface IFloorControlService
{
    /// <summary>
    /// Attempts to obtain the floor for <paramref name="groupId"/> on behalf of
    /// <paramref name="userId"/>. Succeeds when the floor is free or already held by
    /// the same user; fails with <see cref="FloorObtainResult.Conflict"/> when a
    /// different user holds it.
    /// </summary>
    Task<FloorObtainResult> ObtainFloorAsync(string groupId, string userId, CancellationToken ct = default);

    /// <summary>
    /// Attempts to release the floor for <paramref name="groupId"/> on behalf of
    /// <paramref name="userId"/>. Succeeds only when that user is the current holder.
    /// </summary>
    Task<FloorReleaseResult> ReleaseFloorAsync(string groupId, string userId, CancellationToken ct = default);
}
