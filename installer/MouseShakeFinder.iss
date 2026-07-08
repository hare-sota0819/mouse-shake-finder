; Builds a standard Windows installer (Program Files, Start Menu shortcut,
; optional desktop shortcut, uninstaller registered with Windows) around
; the single-file exe produced by `dotnet publish` (see README.md).
;
; Build: run this file with the Inno Setup Compiler (ISCC.exe). Requires
; publish/MouseShakeFinder.exe to already exist.

#define MyAppName "Mouse Shake Finder"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Mouse Shake Finder"
#define MyAppExeName "MouseShakeFinder.exe"
#define MyAppURL "https://github.com/hare-sota0819/mouse-shake-finder"

[Setup]
AppId={{54C19AC1-F884-4F91-9DFB-82EB0A6AB01E}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=..\publish\installer
OutputBaseFilename=MouseShakeFinder-Setup
Compression=lzma
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; "lowest": installs per-user under the user's own Program Files-equivalent
; folder with no UAC prompt, but still honors elevation if the user runs
; the installer as admin. No files are written outside {app} here, so
; this is safe.
PrivilegesRequired=lowest
UninstallDisplayIcon={app}\{#MyAppExeName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "..\publish\MouseShakeFinder.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent

[Code]
// The app enforces a single running instance via a named mutex, and its
// exe file is locked while running -- kill any running copy before
// installing (upgrade case) or uninstalling, so file replacement/removal
// never hits a "file in use" error.
procedure KillRunningApp;
var
  ResultCode: Integer;
begin
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/IM {#MyAppExeName} /F', '', SW_HIDE,
    ewWaitUntilTerminated, ResultCode);
end;

function InitializeSetup(): Boolean;
begin
  KillRunningApp;
  Result := True;
end;

function InitializeUninstall(): Boolean;
begin
  KillRunningApp;
  Result := True;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
  begin
    // Remove the app's own registry data (its crash-recovery marker key).
    RegDeleteKeyIncludingSubkeys(HKCU, 'Software\MouseShakeFinder');
    // Remove only our value from the shared Run key -- never delete the
    // Run key itself, since other applications use it too.
    RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'MouseShakeFinder');
  end;
end;
