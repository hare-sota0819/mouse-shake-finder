namespace MouseShakeFinder.Core;

/// <summary>All tuning knobs for shake detection live here.</summary>
public sealed record ShakeSettings
{
    /// <summary>Direction reversals required inside <see cref="WindowMs"/> to count as a shake.</summary>
    public int MinReversals { get; init; } = 4;

    /// <summary>Rolling time window for counting reversals.</summary>
    public long WindowMs { get; init; } = 500;

    /// <summary>Minimum distance of a movement leg for its reversal to count (filters small corrections/jitter).</summary>
    public double MinSegmentDistance { get; init; } = 50;

    /// <summary>
    /// Minimum average speed (pixels per millisecond) a leg must cover for its
    /// reversal to count. A deliberate "shake to find" gesture is fast; this
    /// keeps ordinary, unhurried mouse movement (which can also reverse
    /// direction, e.g. correcting toward a target) from counting as a shake.
    /// </summary>
    public double MinLegSpeed { get; init; } = 0.5;

    /// <summary>Shake ends after this long without a qualifying reversal.</summary>
    public long QuietMs { get; init; } = 300;

    public static ShakeSettings Default { get; } = new();
}
