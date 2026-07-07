using MouseShakeFinder.Core;

namespace MouseShakeFinder.Core.Tests;

public class ShakeDetectorTests
{
    private static ShakeDetector NewDetector() => new(ShakeSettings.Default);

    // Feed a horizontal zigzag: 60 px legs, one leg every 50 ms.
    // Returns all events emitted.
    private static List<ShakeEvent> FeedZigzag(ShakeDetector d, int legs, double legLength, long legDurationMs, long startMs = 0)
    {
        var events = new List<ShakeEvent>();
        double x = 0;
        long t = startMs;
        events.Add(d.Update(x, 100, t));
        for (int i = 0; i < legs; i++)
        {
            x += (i % 2 == 0) ? legLength : -legLength;
            t += legDurationMs;
            events.Add(d.Update(x, 100, t));
        }
        return events;
    }

    [Fact]
    public void FastZigzagStartsShake()
    {
        var d = NewDetector();
        var events = FeedZigzag(d, legs: 6, legLength: 60, legDurationMs: 50);
        Assert.Contains(ShakeEvent.Started, events);
        Assert.True(d.IsShaking);
    }

    [Fact]
    public void FastStraightMoveDoesNotStartShake()
    {
        var d = NewDetector();
        var events = new List<ShakeEvent>();
        for (int i = 0; i <= 20; i++)
            events.Add(d.Update(i * 100, 100, i * 20));
        Assert.DoesNotContain(ShakeEvent.Started, events);
        Assert.False(d.IsShaking);
    }

    [Fact]
    public void TinyJitterDoesNotStartShake()
    {
        var d = NewDetector();
        // 5 px legs are below MinSegmentDistance (25 px).
        var events = FeedZigzag(d, legs: 12, legLength: 5, legDurationMs: 30);
        Assert.DoesNotContain(ShakeEvent.Started, events);
    }

    [Fact]
    public void SlowZigzagDoesNotStartShake()
    {
        var d = NewDetector();
        // One reversal every 300 ms: never 4 reversals inside the 500 ms window.
        var events = FeedZigzag(d, legs: 10, legLength: 60, legDurationMs: 300);
        Assert.DoesNotContain(ShakeEvent.Started, events);
    }

    [Fact]
    public void QuietPeriodStopsShake()
    {
        var d = NewDetector();
        FeedZigzag(d, legs: 6, legLength: 60, legDurationMs: 50);
        Assert.True(d.IsShaking);
        // Last update was at t=300; quiet threshold is 300 ms.
        Assert.Equal(ShakeEvent.None, d.Tick(400));
        Assert.Equal(ShakeEvent.Stopped, d.Tick(700));
        Assert.False(d.IsShaking);
    }

    [Fact]
    public void VerticalShakeAlsoDetected()
    {
        var d = NewDetector();
        var events = new List<ShakeEvent>();
        double y = 0;
        long t = 0;
        events.Add(d.Update(100, y, t));
        for (int i = 0; i < 6; i++)
        {
            y += (i % 2 == 0) ? 60 : -60;
            t += 50;
            events.Add(d.Update(100, y, t));
        }
        Assert.Contains(ShakeEvent.Started, events);
    }

    [Fact]
    public void ShakeCanRestartAfterStopping()
    {
        var d = NewDetector();
        FeedZigzag(d, legs: 6, legLength: 60, legDurationMs: 50);
        d.Tick(1000); // stops
        Assert.False(d.IsShaking);
        var events = FeedZigzag(d, legs: 6, legLength: 60, legDurationMs: 50, startMs: 2000);
        Assert.Contains(ShakeEvent.Started, events);
    }
}
