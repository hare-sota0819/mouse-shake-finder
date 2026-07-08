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
        // 5 px legs are below MinSegmentDistance (50 px).
        var events = FeedZigzag(d, legs: 12, legLength: 5, legDurationMs: 30);
        Assert.DoesNotContain(ShakeEvent.Started, events);
    }

    [Fact]
    public void SlowZigzagDoesNotStartShake()
    {
        var d = NewDetector();
        // One reversal every 300 ms: too slow to clear MinLegSpeed (60px/300ms
        // = 0.2 px/ms, below the 0.5 px/ms floor), and even ignoring speed,
        // never 4 reversals inside the 500 ms window either.
        var events = FeedZigzag(d, legs: 10, legLength: 60, legDurationMs: 300);
        Assert.DoesNotContain(ShakeEvent.Started, events);
    }

    [Fact]
    public void ModerateSpeedZigzagBelowSpeedThresholdDoesNotStartShake()
    {
        var d = NewDetector();
        // 80px legs clear MinSegmentDistance (50px) and there are plenty of
        // reversals, but 80px/200ms = 0.4 px/ms is just under MinLegSpeed
        // (0.5 px/ms). This is the case that matters most in practice:
        // deliberate everyday mouse movement (e.g. correcting toward a
        // target) can easily cover this much distance while reversing
        // direction without being a "shake to find the cursor" gesture.
        var events = FeedZigzag(d, legs: 8, legLength: 80, legDurationMs: 200);
        Assert.DoesNotContain(ShakeEvent.Started, events);
    }

    [Fact]
    public void DiagonalReversalCountsOnceNotTwice()
    {
        var d = NewDetector();
        // Each leg moves x and y by the same amount at once, so a direction
        // flip changes both axes simultaneously. This must count as exactly
        // one reversal -- if x and y were tracked independently, a single
        // diagonal turn would wrongly register as two reversals, letting a
        // diagonal shake reach MinReversals in half the genuine direction
        // changes a horizontal/vertical shake needs.
        double x = 0, y = 0;
        long t = 0;
        var events = new List<ShakeEvent> { d.Update(x, y, t) };

        // 4 legs = 3 genuine reversals: one short of MinReversals (4).
        for (int i = 0; i < 4; i++)
        {
            double step = (i % 2 == 0) ? 60 : -60;
            x += step;
            y += step;
            t += 50;
            events.Add(d.Update(x, y, t));
        }
        Assert.DoesNotContain(ShakeEvent.Started, events);

        // One more leg = the 4th genuine reversal: now it should start.
        x += 60;
        y += 60;
        t += 50;
        events.Add(d.Update(x, y, t));
        Assert.Contains(ShakeEvent.Started, events);
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

    [Fact]
    public void StaleLegStateAfterStopDoesNotGrantPhantomReversal()
    {
        var d = NewDetector();
        // Run a shake to completion. Its last leg (leg 6, i=5) moves in the
        // negative-x direction, so the detector's in-progress leg state
        // ends pointed at (-60, 0) (>= MinSegmentDistance).
        FeedZigzag(d, legs: 6, legLength: 60, legDurationMs: 50);
        Assert.True(d.IsShaking);
        Assert.Equal(ShakeEvent.Stopped, d.Tick(1000));
        Assert.False(d.IsShaking);

        // Feed a fresh zigzag of 4 legs starting in the +x direction, i.e.
        // the opposite of the stale -x direction left over from the previous
        // shake. With correctly reset state, the first leg of a fresh
        // zigzag can never itself be a qualifying reversal (no prior
        // direction to reverse from), so 4 legs yield only 3 genuine
        // reversals -- one short of MinReversals (4), so no shake starts.
        // With the bug (no reset on stop), the stale _direction=-1 /
        // _segmentDistance=60 makes the very first post-stop leg (+x)
        // itself qualify as a phantom reversal, pushing the total to 4
        // and incorrectly starting a shake.
        var events = FeedZigzag(d, legs: 4, legLength: 60, legDurationMs: 50, startMs: 2000);

        Assert.DoesNotContain(ShakeEvent.Started, events);
        Assert.False(d.IsShaking);
    }
}
