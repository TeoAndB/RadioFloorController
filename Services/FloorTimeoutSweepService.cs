using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RadioFloorController.Data;
using RadioFloorController.Domain;

namespace RadioFloorController.Services;

/// <summary>
/// Background sweep that proactively frees floor grants whose hold has passed its
/// <see cref="FloorGrantEntity.ExpiresAt"/> deadline, so a group isn't left blocked
/// indefinitely just because nobody happens to call obtain/release for it again after the
/// timeout. This complements the lazy expiry check in <see cref="FloorControlService"/>
/// (which only reclaims an expired hold when someone next tries to obtain that same group) —
/// together they ensure every expired hold is freed within, at most, one
/// <see cref="FloorControlOptions.SweepInterval"/>.
/// </summary>
/// <remarks>
/// Registered as a singleton-lifetime <see cref="BackgroundService"/>, but <see cref="AppDbContext"/>
/// is scoped, so a fresh <see cref="IServiceScope"/> (and therefore a fresh <see cref="AppDbContext"/>)
/// is created and disposed on every tick rather than holding a scoped context across ticks.
/// Each tick performs a single atomic conditional bulk <c>ExecuteUpdateAsync</c> over all
/// expired rows, so the database still resolves any race against a concurrent Obtain/Release
/// call for the same group. A failure on one tick is logged and swallowed so it doesn't take
/// down the sweep loop.
/// </remarks>
public sealed class FloorTimeoutSweepService(
    IServiceScopeFactory scopeFactory,
    IOptions<FloorControlOptions> options,
    ILogger<FloorTimeoutSweepService> logger) : BackgroundService
{
    private const string TimedOutReleaseReason = "TimedOut";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.Value.SweepInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await SweepExpiredGrantsAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // A single failed tick (e.g. transient DB connectivity issue) must not crash
                // the sweep loop — log it and try again on the next tick.
                logger.LogError(ex, "Floor timeout sweep tick failed; will retry on the next tick.");
            }
        }
    }

    private async Task SweepExpiredGrantsAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var now = DateTimeOffset.UtcNow;

        var affected = await db.FloorGrants
            .Where(g => g.HolderUserId != null && g.ExpiresAt <= now)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(g => g.LastHolderUserId, g => g.HolderUserId)
                .SetProperty(g => g.HolderUserId, (string?)null)
                .SetProperty(g => g.ObtainedAt, (DateTimeOffset?)null)
                .SetProperty(g => g.ExpiresAt, (DateTimeOffset?)null)
                .SetProperty(g => g.LastReleaseReason, TimedOutReleaseReason)
                .SetProperty(g => g.LastReleasedAt, g => g.ExpiresAt), ct);

        if (affected > 0)
        {
            logger.LogInformation("Floor timeout sweep auto-released {Count} expired floor grant(s).", affected);
        }
    }
}
