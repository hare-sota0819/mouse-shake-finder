@echo off
setlocal
cd /d C:\

set ISCC="%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe"
if not exist %ISCC% set ISCC="%ProgramFiles%\Inno Setup 6\ISCC.exe"
if not exist %ISCC% set ISCC="%LocalAppData%\Programs\Inno Setup 6\ISCC.exe"
if not exist %ISCC% (
    echo Inno Setup 6 not found. Install it: winget install JRSoftware.InnoSetup
    exit /b 1
)

%ISCC% "%~dp0MouseShakeFinder.iss"
