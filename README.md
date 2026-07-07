# Mouse Shake Finder

Shake your mouse, find your cursor — the macOS "shake to locate" feature
for Windows. When you rapidly shake the mouse, the system pointer grows
to maximum size; when you stop, it returns to your original size.

## Install

None. Download/build `MouseShakeFinder.exe` and run it. An icon appears
in the system tray.

## Tray menu

- **Pause / Resume** — temporarily disable detection
- **Start on boot** — toggle automatic start at Windows login
- **Exit** — quit (cursor size is always restored)

## Build (from WSL or Windows, .NET 10 SDK)

    dotnet publish src/MouseShakeFinder -c Release -p:PublishSingleFile=true -o publish

## Verify

    ./scripts/verify.sh

## How it works

A low-level mouse hook feeds cursor positions to a small state machine
(4+ direction reversals within 500 ms = shake). On shake it raises the
Windows accessibility pointer size (Settings > Accessibility > Mouse
pointer) to 15 and restores your original size afterwards — also after
a crash, via a registry marker.
