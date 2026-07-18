namespace RadioFloorController.Domain;

/// <summary>
/// Outcome of attempting to release the floor for a radio group. Exactly two possible
/// outcomes — pattern-match/switch on the concrete subtype, e.g.:
/// <code>
/// FloorReleaseResult result = await service.ReleaseFloorAsync(groupId, userId, ct);
/// var response = result switch
/// {
///     FloorReleaseResult.Released =&gt; ...,
///     FloorReleaseResult.NotHolder =&gt; ...,
/// };
/// </code>
/// This is a closed hierarchy (private constructor): no further subtypes are possible
/// outside this file.
/// </summary>
public abstract record FloorReleaseResult
{
    private FloorReleaseResult()
    {
    }

    /// <summary>The caller held the floor and it is now free.</summary>
    public sealed record Released : FloorReleaseResult;

    /// <summary>
    /// The caller does not currently hold the floor for the group — either nobody
    /// holds it, or a different user does.
    /// </summary>
    public sealed record NotHolder : FloorReleaseResult;
}
