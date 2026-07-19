namespace RadioFloorController.Domain;

/// <summary>
/// Effective, point-in-time status of who (if anyone) currently holds the floor for a radio
/// group. "Effective" means this reflects an already-passed <c>ExpiresAt</c> deadline as
/// not-held even if the background sweep has not yet physically updated the underlying row —
/// callers should never observe a holder whose time has already run out. Exactly two possible
/// outcomes — pattern-match/switch on the concrete subtype, e.g.:
/// <code>
/// FloorHolderResult result = await service.GetCurrentHolderAsync(groupId, ct);
/// var response = result switch
/// {
///     FloorHolderResult.Held(var holderUserId, var obtainedAt, var expiresAt) =&gt; ...,
///     FloorHolderResult.NotHeld =&gt; ...,
/// };
/// </code>
/// This is a closed hierarchy (private constructor): no further subtypes are possible
/// outside this file.
/// </summary>
public abstract record FloorHolderResult
{
    private FloorHolderResult()
    {
    }

    /// <summary>The floor is currently held and has not yet passed its expiry deadline.</summary>
    /// <param name="HolderUserId">The user id of the current holder.</param>
    /// <param name="ObtainedAt">When the current holder obtained the floor.</param>
    /// <param name="ExpiresAt">
    /// The deadline at which this hold will auto-expire unless released or renewed first.
    /// </param>
    public sealed record Held(string HolderUserId, DateTimeOffset ObtainedAt, DateTimeOffset ExpiresAt)
        : FloorHolderResult;

    /// <summary>
    /// Nobody currently, validly holds the floor — covers a group that has never had a floor
    /// grant at all, a floor that was manually released, and a floor whose hold already timed
    /// out (lazily detected here or already swept).
    /// </summary>
    public sealed record NotHeld : FloorHolderResult;
}
