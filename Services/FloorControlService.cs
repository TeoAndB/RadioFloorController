using Microsoft.EntityFrameworkCore;
using Npgsql;
using RadioFloorController.Data;
using RadioFloorController.Domain;

namespace RadioFloorController.Services;

/// <summary>
/// <see cref="IFloorControlService"/> implementation backed directly by <see cref="AppDbContext"/>.
/// </summary>
/// <remarks>
/// Concurrency safety: the "is anyone holding it" check and the grant/release itself are
/// collapsed into a single atomic conditional UPDATE (<c>ExecuteUpdateAsync</c> translates to
/// one SQL UPDATE with the ownership check in its WHERE clause), so the database — not
/// application code — resolves races between concurrent callers for the same group; exactly
/// one caller's UPDATE can ever match and affect the row. The one case a conditional UPDATE
/// can't cover is the very first obtain for a group, which has no row yet: that path inserts
/// a new row and relies on the primary key (GroupId) to reject a concurrent duplicate insert
/// (detected via the Postgres unique-violation SQL state, not any <see cref="DbUpdateException"/>),
/// falling back to the same conditional UPDATE once a row exists. A bounded retry loop covers
/// the remaining race window between a failed claim and the follow-up read of current state.
/// </remarks>
public sealed class FloorControlService(AppDbContext db) : IFloorControlService
{
    private const int MaxObtainAttempts = 5;

    public async Task<FloorObtainResult> ObtainFloorAsync(string groupId, string userId, CancellationToken ct = default)
    {
        for (var attempt = 0; attempt < MaxObtainAttempts; attempt++)
        {
            var now = DateTimeOffset.UtcNow;

            if (await TryClaimExistingGrantAsync(groupId, userId, now, ct))
            {
                return new FloorObtainResult.Obtained();
            }

            // No existing row matched the conditional claim: either this group has never had a
            // floor grant row, a different user currently holds it, or the row's state changed
            // concurrently between the claim attempt and this read — re-check before deciding.
            var existing = await db.FloorGrants.AsNoTracking()
                .FirstOrDefaultAsync(g => g.GroupId == groupId, ct);

            if (existing is null)
            {
                db.FloorGrants.Add(new FloorGrantEntity
                {
                    GroupId = groupId,
                    HolderUserId = userId,
                    ObtainedAt = now
                });

                try
                {
                    await db.SaveChangesAsync(ct);
                    return new FloorObtainResult.Obtained();
                }
                catch (DbUpdateException ex) when (ex.InnerException is PostgresException
                    { SqlState: PostgresErrorCodes.UniqueViolation })
                {
                    // A concurrent first-obtain for the same group won the insert race.
                    // The row now exists; retry the atomic conditional claim.
                    db.ChangeTracker.Clear();
                    continue;
                }
            }

            if (existing.HolderUserId is null || existing.HolderUserId == userId)
            {
                // The row was freed, or claimed by this same user, concurrently between our
                // failed claim attempt and this read. Retry rather than reporting a false
                // conflict against ourselves (or a floor nobody holds).
                continue;
            }

            return new FloorObtainResult.Conflict(existing.HolderUserId);
        }

        throw new InvalidOperationException(
            $"Could not resolve floor obtain for group '{groupId}' after {MaxObtainAttempts} attempts due to persistent contention.");
    }

    public async Task<FloorReleaseResult> ReleaseFloorAsync(string groupId, string userId, CancellationToken ct = default)
    {
        var affected = await db.FloorGrants
            .Where(g => g.GroupId == groupId && g.HolderUserId == userId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(g => g.HolderUserId, (string?)null)
                .SetProperty(g => g.ObtainedAt, (DateTimeOffset?)null), ct);

        return affected == 1 ? new FloorReleaseResult.Released() : new FloorReleaseResult.NotHolder();
    }

    /// <summary>
    /// Atomically claims an existing floor-grant row for <paramref name="userId"/>, but only
    /// if it is currently free or already held by that same user. Returns whether the claim
    /// matched (and therefore affected) a row.
    /// </summary>
    private async Task<bool> TryClaimExistingGrantAsync(string groupId, string userId, DateTimeOffset now, CancellationToken ct)
    {
        var affected = await db.FloorGrants
            .Where(g => g.GroupId == groupId && (g.HolderUserId == null || g.HolderUserId == userId))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(g => g.HolderUserId, userId)
                .SetProperty(g => g.ObtainedAt, now), ct);

        return affected == 1;
    }
}
