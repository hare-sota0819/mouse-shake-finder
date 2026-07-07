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
    private sealed class AxisTracker
    {
        private readonly double _minSegment;
        private int _direction; // -1, 0 (unknown yet), +1
        private double _segmentDistance;

        public AxisTracker(double minSegment) => _minSegment = minSegment;

        /// <summary>Returns true when this delta completes a qualifying direction reversal.</summary>
        public bool Update(double delta)
        {
            if (delta == 0)
            {
                return false;
            }

            int direction = delta > 0 ? 1 : -1;
            if (direction == _direction)
            {
                _segmentDistance += Math.Abs(delta);
                return false;
            }

            bool qualifies = _direction != 0 && _segmentDistance >= _minSegment;
            _direction = direction;
            _segmentDistance = Math.Abs(delta);
            return qualifies;
        }
    }

    private readonly ShakeSettings _settings;
    private readonly AxisTracker _horizontal;
    private readonly AxisTracker _vertical;
    private readonly Queue<long> _reversalTimes = new();
    private double _lastX;
    private double _lastY;
    private bool _hasLastPosition;
    private long _lastReversalMs;

    public bool IsShaking { get; private set; }

    public ShakeDetector(ShakeSettings settings)
    {
        _settings = settings;
        _horizontal = new AxisTracker(settings.MinSegmentDistance);
        _vertical = new AxisTracker(settings.MinSegmentDistance);
    }

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

        if (_horizontal.Update(dx))
        {
            RecordReversal(nowMs);
        }
        if (_vertical.Update(dy))
        {
            RecordReversal(nowMs);
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
            return ShakeEvent.Stopped;
        }

        return ShakeEvent.None;
    }

    private void RecordReversal(long nowMs)
    {
        _reversalTimes.Enqueue(nowMs);
        _lastReversalMs = nowMs;
    }
}
