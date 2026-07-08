using System.Collections.Concurrent;
using MouseShakeFinder.Core;

namespace MouseShakeFinder;

public sealed class TrayApp : ApplicationContext
{
    private readonly NotifyIcon _trayIcon;
    private readonly MouseHook _mouseHook;
    private readonly System.Windows.Forms.Timer _quietTimer;
    private readonly ShakeDetector _detector = new(ShakeSettings.Default);
    private readonly CursorScaler _scaler = new();
    private readonly int _originalSize;
    private bool _paused;
    private bool _enlarged;

    // The mouse hook callback runs synchronously on this thread's message
    // pump, and Windows' whole mouse-input pipeline waits for it to return.
    // CursorScaler.Enlarge/Restore call SystemParametersInfo, which
    // broadcasts a system-wide setting change and can take a noticeable
    // amount of time -- doing that inline froze real cursor movement for
    // the duration of the call. Running it on a dedicated background
    // thread keeps the hook callback (and therefore the physical cursor)
    // responsive at all times.
    private readonly BlockingCollection<Action> _scalerQueue = new();
    private readonly Thread _scalerThread;

    public TrayApp()
    {
        // If a previous run crashed while enlarged, fix the cursor first,
        // THEN read the (now correct) current size as the original.
        _scaler.RestoreLeftoverIfAny();
        _originalSize = _scaler.ReadCurrentSize();

        _scalerThread = new Thread(RunScalerQueue) { IsBackground = true, Name = "CursorScalerWorker" };
        _scalerThread.Start();

        var pauseItem = new ToolStripMenuItem("Pause");
        pauseItem.Click += (_, _) =>
        {
            _paused = !_paused;
            pauseItem.Text = _paused ? "Resume" : "Pause";
            if (_paused)
            {
                RestoreCursor();
            }
        };

        var autoStartItem = new ToolStripMenuItem("Start on boot")
        {
            Checked = AutoStart.IsEnabled(),
            CheckOnClick = true,
        };
        autoStartItem.CheckedChanged += (_, _) => AutoStart.SetEnabled(autoStartItem.Checked);

        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += (_, _) => ExitThread();

        var menu = new ContextMenuStrip();
        menu.Items.Add(pauseItem);
        menu.Items.Add(autoStartItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exitItem);

        _trayIcon = new NotifyIcon
        {
            // The app's own icon (embedded via <ApplicationIcon> in the
            // csproj) is reused here so the exe, Start Menu shortcut, and
            // tray icon all show the same image from one source file.
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application,
            Text = "Mouse Shake Finder",
            ContextMenuStrip = menu,
            Visible = true,
        };

        _mouseHook = new MouseHook();
        _mouseHook.MouseMoved += OnMouseMoved;

        // Ends the shake even when the mouse stops moving entirely.
        _quietTimer = new System.Windows.Forms.Timer { Interval = 50 };
        _quietTimer.Tick += (_, _) => HandleShakeEvent(_detector.Tick(Environment.TickCount64));
        _quietTimer.Start();
    }

    private void RunScalerQueue()
    {
        foreach (var action in _scalerQueue.GetConsumingEnumerable())
        {
            action();
        }
    }

    private void EnqueueScalerAction(Action action)
    {
        try
        {
            _scalerQueue.Add(action);
        }
        catch (InvalidOperationException)
        {
            // Queue already completed (app is shutting down) -- the exit
            // path performs its own final synchronous restore, so this is
            // safe to drop.
        }
    }

    private void OnMouseMoved(int x, int y, long nowMs)
    {
        if (!_paused)
        {
            HandleShakeEvent(_detector.Update(x, y, nowMs));
        }
    }

    private void HandleShakeEvent(ShakeEvent shakeEvent)
    {
        if (shakeEvent == ShakeEvent.Started)
        {
            _enlarged = true;
            EnqueueScalerAction(() => _scaler.Enlarge(_originalSize));
        }
        else if (shakeEvent == ShakeEvent.Stopped)
        {
            RestoreCursor();
        }
    }

    private void RestoreCursor()
    {
        if (_enlarged)
        {
            _enlarged = false;
            EnqueueScalerAction(() => _scaler.Restore(_originalSize));
        }
    }

    protected override void ExitThreadCore()
    {
        _quietTimer.Stop();
        _mouseHook.Dispose();

        // Let any in-flight background enlarge/restore finish, then force
        // one final, synchronous, direct (non-queued) restore -- this is
        // what actually guarantees the cursor is back to normal before the
        // process exits, regardless of what was queued or mid-flight above.
        _scalerQueue.CompleteAdding();
        _scalerThread.Join(TimeSpan.FromSeconds(2));
        _enlarged = false;
        _scaler.Restore(_originalSize);

        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        base.ExitThreadCore();
    }
}
