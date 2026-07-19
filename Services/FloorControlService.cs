using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
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
///
/// Hold timeouts extend this same pattern rather than replacing it: an expired hold (current
/// time past the row's <c>ExpiresAt</c>) is claimable exactly like a free row, and the "was
/// this claim a reclaim-after-timeout" distinction is captured by attempting two conditional
/// UPDATEs in order — first "claim an expired, different-user hold" (which also stamps
/// <c>LastHolderUserId</c>/<c>LastReleaseReason</c>/<c>LastReleasedAt</c> for the outgoing
/// holder), then "claim a free-or-already-mine" row — each individually atomic, so the
/// database still resolves which one (if either) applies for a given caller. A separate
/// background sweep (<see cref="FloorTimeoutSweepService"/>) proactively performs the
/// equivalent bulk release so groups aren't left "expired but not yet reclaimed" indefinitely
/// just because nobody calls Obtain/Release for them again.
/// </remarks>
public sealed class FloorControlService(AppDbContext db, IOptions<FloorControlOptions> options) : IFloorControlService
{
    private const int MaxObtainAttempts = 5;
    private const string ManualReleaseReason = "Manual";
    private const string TimedOutReleaseReason = "TimedOut";

    private readonly FloorControlOptions _options = options.Value;

    public async Task<FloorObtainResult> ObtainFloorAsync(string groupId, string userId, CancellationToken ct = default)
    {
        for (var attempt = 0; attempt < MaxObtainAttempts; attempt++)
        {
            var now = DateTimeOffset.UtcNow;

            if (await TryClaimExistingGrantAsync(groupId, userId, now, ct))
            {
                return new FloorObtainResult.Obtained();
            }

            // No existing row matched either conditional claim: either this group has never had
            // a floor grant row, a different user's still-valid hold blocks us, or the row's
            // state changed concurrently between the claim attempts and this read — re-check
            // before deciding.
            var existing = await db.FloorGrants.AsNoTracking()
                .FirstOrDefaultAsync(g => g.GroupId == groupId, ct);

            if (existing is null)
            {
                db.FloorGrants.Add(new FloorGrantEntity
                {
                    GroupId = groupId,
                    HolderUserId = userId,
                    ObtainedAt = now,
                    ExpiresAt = now + _options.HoldTimeout
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

            if (existing.HolderUserId is null || existing.HolderUserId == userId || existing.ExpiresAt <= now)
            {
                // The row was freed, claimed by this same user, or its hold expired,
                // concurrently between our failed claim attempts and this read. Retry rather
                // than reporting a false conflict against a floor that is (or should be) ours.
                continue;
            }

            return new FloorObtainResult.Conflict(existing.HolderUserId);
        }

        throw new InvalidOperationException(
            $"Could not resolve floor obtain for group '{groupId}' after {MaxObtainAttempts} attempts due to persistent contention.");
    }

    public async Task<FloorReleaseResult> ReleaseFloorAsync(string groupId, string userId, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;

        var affected = await db.FloorGrants
            .Where(g => g.GroupId == groupId && g.HolderUserId == userId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(g => g.HolderUserId, (string?)null)
                .SetProperty(g => g.ObtainedAt, (DateTimeOffset?)null)
                .SetProperty(g => g.ExpiresAt, (DateTimeOffset?)null)
                .SetProperty(g => g.LastReleaseReason, ManualReleaseReason)
                .SetProperty(g => g.LastReleasedAt, now)
                .SetProperty(g => g.LastHolderUserId, userId), ct);

        if (affected == 1)
        {
            return new FloorReleaseResult.Released();
        }

        // The conditional UPDATE didn't match — this user isn't the current holder. Read the
        // row (outside the hot obtain path, so a plain read-then-branch is fine here) to
        // distinguish "your own hold already timed out" from the generic "not the holder".
        var existing = await db.FloorGrants.AsNoTracking()
            .FirstOrDefaultAsync(g => g.GroupId == groupId, ct);

        var releaserWasTimedOutHolder = existing is not null
            && existing.LastHolderUserId == userId
            && existing.LastReleaseReason == TimedOutReleaseReason;

        return releaserWasTimedOutHolder
            ? new FloorReleaseResult.TimedOut()
            : new FloorReleaseResult.NotHolder();
    }

    public async Task<FloorHolderResult> GetCurrentHolderAsync(string groupId, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;

        var grant = await db.FloorGrants.AsNoTracking()
            .FirstOrDefaultAsync(g => g.GroupId == groupId, ct);

        if (grant is null)
        {
            return new FloorHolderResult.NotHeld();
        }

        if (grant.HolderUserId is not null && grant.ExpiresAt is { } expiresAt)
        {
            if (expiresAt > now)
            {
                return new FloorHolderResult.Held(grant.HolderUserId, grant.ObtainedAt ?? now, expiresAt);
            }

            // Held per the row, but already past its deadline: report the effective (expired)
            // state without mutating the row here — the sweep (or the next obtain) will
            // physically clear it.
            return new FloorHolderResult.NotHeld();
        }

        return new FloorHolderResult.NotHeld();
    }

    /// <summary>
    /// Atomically claims an existing floor-grant row for <paramref name="userId"/>: either
    /// because a different user's hold has expired (reclaiming it and recording that outgoing
    /// holder's timeout in the same UPDATE), or because the row is already free or already held
    /// by this same user. Each case is a single atomic conditional UPDATE, so only one of the
    /// two — and only for one caller — can ever match a given row state. Returns whether either
    /// claim matched (and therefore affected) a row.
    /// </summary>
    private async Task<bool> TryClaimExistingGrantAsync(string groupId, string userId, DateTimeOffset now, CancellationToken ct)
    {
        var expiresAt = now + _options.HoldTimeout;

        // Case 1: a different user currently holds it, but their hold has passed its
        // ExpiresAt deadline. Treat as free and, in the same statement, record the timeout
        // for the outgoing holder. The SET clauses referencing g.HolderUserId/g.ExpiresAt
        // evaluate against the row's pre-update values, so this is safe even though the same
        // statement also overwrites HolderUserId/ExpiresAt with the new holder's values.
        var reclaimedExpired = await db.FloorGrants
            .Where(g => g.GroupId == groupId
                && g.HolderUserId != null
                && g.HolderUserId != userId
                && g.ExpiresAt <= now)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(g => g.LastHolderUserId, g => g.HolderUserId)
                .SetProperty(g => g.LastReleaseReason, TimedOutReleaseReason)
                .SetProperty(g => g.LastReleasedAt, g => g.ExpiresAt)
                .SetProperty(g => g.HolderUserId, userId)
                .SetProperty(g => g.ObtainedAt, now)
                .SetProperty(g => g.ExpiresAt, expiresAt), ct);

        if (reclaimedExpired == 1)
        {
            return true;
        }

        // Case 2: the floor is already free, or already held by this same user (idempotent
        // re-obtain, which also renews the expiry deadline).
        var claimedFreeOrSelf = await db.FloorGrants
            .Where(g => g.GroupId == groupId && (g.HolderUserId == null || g.HolderUserId == userId))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(g => g.HolderUserId, userId)
                .SetProperty(g => g.ObtainedAt, now)
                .SetProperty(g => g.ExpiresAt, expiresAt), ct);

        return claimedFreeOrSelf == 1;
    }
}
