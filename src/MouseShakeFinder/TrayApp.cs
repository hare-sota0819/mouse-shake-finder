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

    public TrayApp()
    {
        // If a previous run crashed while enlarged, fix the cursor first,
        // THEN read the (now correct) current size as the original.
        _scaler.RestoreLeftoverIfAny();
        _originalSize = _scaler.ReadCurrentSize();

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
            Icon = SystemIcons.Application,
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
            _scaler.Enlarge(_originalSize);
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
            _scaler.Restore(_originalSize);
        }
    }

    protected override void ExitThreadCore()
    {
        _quietTimer.Stop();
        _mouseHook.Dispose();
        RestoreCursor();
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        base.ExitThreadCore();
    }
}
