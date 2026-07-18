namespace RadioFloorController.Domain;

/// <summary>
/// Outcome of attempting to obtain the floor for a radio group. Exactly two possible
/// outcomes — pattern-match/switch on the concrete subtype, e.g.:
/// <code>
/// FloorObtainResult result = await service.ObtainFloorAsync(groupId, userId, ct);
/// var response = result switch
/// {
///     FloorObtainResult.Obtained =&gt; ...,
///     FloorObtainResult.Conflict(var holderId) =&gt; ...,
/// };
/// </code>
/// This is a closed hierarchy (private constructor): no further subtypes are possible
/// outside this file.
/// </summary>
public abstract record FloorObtainResult
{
    private FloorObtainResult()
    {
    }

    /// <summary>
    /// The caller now holds the floor for the group. Returned both when the floor was
    /// free and when the caller already held it (re-obtaining is idempotent, not a conflict).
    /// </summary>
    public sealed record Obtained : FloorObtainResult;

    /// <summary>A different user already holds the floor for the group.</summary>
    /// <param name="HolderUserId">The user id of the current holder.</param>
    public sealed record Conflict(string HolderUserId) : FloorObtainResult;
}
