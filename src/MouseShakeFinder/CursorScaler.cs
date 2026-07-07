using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace MouseShakeFinder;

/// <summary>
/// Changes the Windows accessibility mouse pointer size (Settings >
/// Accessibility > Mouse pointer, scale 1-15). Same mechanism the
/// Settings app uses: registry value + SystemParametersInfo(0x2029)
/// with the pixel size (size 1 = 32 px, each step +16 px).
/// </summary>
public sealed class CursorScaler
{
    private const string AccessibilityKeyPath = @"Software\Microsoft\Accessibility";
    private const string CursorSizeValueName = "CursorSize";
    private const string AppKeyPath = @"Software\MouseShakeFinder";
    private const string OriginalSizeValueName = "OriginalCursorSize";

    private const int MinSize = 1;
    private const int MaxSize = 15;
    public const int EnlargedSize = MaxSize;

    // Undocumented but stable since Windows 10 1903; what the Settings app calls.
    private const uint SPI_SETCURSORSIZE = 0x2029;
    private const uint SPIF_UPDATEINIFILE = 0x01;
    private const uint SPIF_SENDCHANGE = 0x02;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, nint pvParam, uint fWinIni);

    public int ReadCurrentSize()
    {
        using var key = Registry.CurrentUser.OpenSubKey(AccessibilityKeyPath);
        return key?.GetValue(CursorSizeValueName) is int size
            ? Math.Clamp(size, MinSize, MaxSize)
            : MinSize;
    }

    public void Enlarge(int originalSize)
    {
        using var appKey = Registry.CurrentUser.CreateSubKey(AppKeyPath);
        appKey.SetValue(OriginalSizeValueName, originalSize);
        Apply(EnlargedSize);
    }

    public void Restore(int originalSize)
    {
        Apply(originalSize);
        using var appKey = Registry.CurrentUser.CreateSubKey(AppKeyPath);
        appKey.DeleteValue(OriginalSizeValueName, throwOnMissingValue: false);
    }

    /// <summary>
    /// If a previous run crashed while the cursor was enlarged, the marker
    /// value is still present: restore the user's size it recorded.
    /// </summary>
    public void RestoreLeftoverIfAny()
    {
        using var appKey = Registry.CurrentUser.OpenSubKey(AppKeyPath, writable: true);
        if (appKey?.GetValue(OriginalSizeValueName) is int leftover)
        {
            Apply(Math.Clamp(leftover, MinSize, MaxSize));
            appKey.DeleteValue(OriginalSizeValueName, throwOnMissingValue: false);
        }
    }

    private static void Apply(int size)
    {
        size = Math.Clamp(size, MinSize, MaxSize);
        using var key = Registry.CurrentUser.CreateSubKey(AccessibilityKeyPath);
        key.SetValue(CursorSizeValueName, size);
        int pixels = 16 + size * 16;
        SystemParametersInfo(SPI_SETCURSORSIZE, 0, pixels, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);
    }
}
