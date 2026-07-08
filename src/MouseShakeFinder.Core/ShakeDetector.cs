namespace MouseShakeFinder.Core;

public enum ShakeEvent
{
    None,
    Started,
    Stopped,
}

/// <summary>
/// Pure shake-detection state machine. Feed it timestamped cursor positions
/// via Update(); call Tick() periodically so a shake also ends when the
/// mouse stops moving entirely.
/// </summary>
public sealed class ShakeDetector
{
    private readonly ShakeSettings _settings;
    private readonly Queue<long> _reversalTimes = new();

    private double _lastX;
    private double _lastY;
    private bool _hasLastPosition;
    private long _lastReversalMs;

    // The current "leg": the movement vector accumulated since the last
    // direction reversal (or since tracking started). A reversal is a move
    // whose direction is more than 90 degrees from this accumulated
    // direction -- checked once for the combined 2D vector, not per axis,
    // so a single diagonal direction change can't be double-counted as two
    // reversals the way independent x/y trackers would.
    private double _legDx;
    private double _legDy;
    private long _legStartMs;
    private bool _legActive;

    public bool IsShaking { get; private set; }

    public ShakeDetector(ShakeSettings settings) => _settings = settings;

    public ShakeEvent Update(double x, double y, long nowMs)
    {
        if (!_hasLastPosition)
        {
            _lastX = x;
            _lastY = y;
            _hasLastPosition = true;
            return Tick(nowMs);
        }

        double dx = x - _lastX;
        double dy = y - _lastY;
        _lastX = x;
        _lastY = y;

        if (dx != 0 || dy != 0)
        {
            ProcessDelta(dx, dy, nowMs);
        }

        while (_reversalTimes.Count > 0 && nowMs - _reversalTimes.Peek() > _settings.WindowMs)
        {
            _reversalTimes.Dequeue();
        }

        if (!IsShaking && _reversalTimes.Count >= _settings.MinReversals)
        {
            IsShaking = true;
            return ShakeEvent.Started;
        }

        return Tick(nowMs);
    }

    public ShakeEvent Tick(long nowMs)
    {
        if (IsShaking && nowMs - _lastReversalMs >= _settings.QuietMs)
        {
            IsShaking = false;
            _reversalTimes.Clear();
            _legActive = false;
            return ShakeEvent.Stopped;
        }

        return ShakeEvent.None;
    }

    private void ProcessDelta(double dx, double dy, long nowMs)
    {
        if (!_legActive)
        {
            StartLeg(dx, dy, nowMs);
            return;
        }

        double dot = (_legDx * dx) + (_legDy * dy);
        if (dot < 0)
        {
            if (QualifiesAsReversal(nowMs))
            {
                RecordReversal(nowMs);
            }
            StartLeg(dx, dy, nowMs);
        }
        else
        {
            _legDx += dx;
            _legDy += dy;
        }
    }

    private bool QualifiesAsReversal(long nowMs)
    {
        double legDistance = Math.Sqrt((_legDx * _legDx) + (_legDy * _legDy));
        if (legDistance < _settings.MinSegmentDistance)
        {
            return false;
        }

        long legDurationMs = Math.Max(1, nowMs - _legStartMs);
        double speed = legDistance / legDurationMs;
        return speed >= _settings.MinLegSpeed;
    }

    private void StartLeg(double dx, double dy, long nowMs)
    {
        _legDx = dx;
        _legDy = dy;
        _legStartMs = nowMs;
        _legActive = true;
    }

    private void RecordReversal(long nowMs)
    {
        _reversalTimes.Enqueue(nowMs);
        _lastReversalMs = nowMs;
    }
}
