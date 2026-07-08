# Mouse Shake Finder

Shake your mouse, find your cursor — the macOS "shake to locate" feature
for Windows. When you rapidly shake the mouse, the system pointer grows
to maximum size; when you stop, it returns to your original size.

## Install

Download `MouseShakeFinder-Setup.exe` from the [latest release](../../releases/latest)
and run it — standard Windows installer, no admin rights required. It adds
a Start Menu entry and an uninstaller; a desktop shortcut is optional.

Prefer no installer? Download `MouseShakeFinder.exe` instead and run it
directly — same app, portable, nothing to install or uninstall.

Either way, an icon appears in the system tray once it's running.

## Tray menu

- **Pause / Resume** — temporarily disable detection
- **Start on boot** — toggle automatic start at Windows login
- **Exit** — quit (cursor size is always restored)

## Build (from WSL or Windows, .NET 10 SDK)

    dotnet publish src/MouseShakeFinder -c Release -p:PublishSingleFile=true -o publish

Produces the portable `publish/MouseShakeFinder.exe`.

### Building the installer

Requires [Inno Setup 6](https://jrsoftware.org/isinfo.php) (Windows only)
and the portable exe already published (previous step). Compile
`installer/MouseShakeFinder.iss` with the Inno Setup Compiler (`ISCC.exe`);
the result is `publish/installer/MouseShakeFinder-Setup.exe`.

## Verify

    ./scripts/verify.sh

## How it works

A low-level mouse hook feeds cursor positions to a small state machine
(4+ direction reversals within 500 ms = shake). On shake it raises the
Windows accessibility pointer size (Settings > Accessibility > Mouse
pointer) to 15 and restores your original size afterwards — also after
a crash, via a registry marker.
