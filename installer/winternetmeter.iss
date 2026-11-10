; Inno Setup script for Winternet Meter.
; Built by .github/workflows/release.yml:
;   ISCC /DAppVersion=2.0.0 /DArch=win-x64 /DArchAllowed=x64compatible /DSourceExe=..\publish\win-x64\WinternetMeter.exe winternetmeter.iss
;
; Per-user install (no admin prompt) to %LocalAppData%\Programs\WinternetMeter.

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif
#ifndef Arch
  #define Arch "win-x64"
#endif
#ifndef ArchAllowed
  #define ArchAllowed "x64compatible"
#endif
#ifndef SourceExe
  #define SourceExe "..\publish\win-x64\WinternetMeter.exe"
#endif

[Setup]
AppId={{6E3A1C52-9B7D-4F0E-A1C4-2D8B5E7F9A31}
AppName=Winternet Meter
AppVersion={#AppVersion}
AppVerName=Winternet Meter {#AppVersion}
AppPublisher=Rakibul Hasan
AppPublisherURL=https://github.com/rakibulx33/WinternetMeter
AppSupportURL=https://github.com/rakibulx33/WinternetMeter/issues
DefaultDirName={localappdata}\Programs\WinternetMeter
DisableDirPage=auto
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed={#ArchAllowed}
ArchitecturesInstallIn64BitMode={#ArchAllowed}
MinVersion=10.0.17763
OutputDir=Output
OutputBaseFilename=WinternetMeter-{#AppVersion}-{#Arch}-setup
SetupIconFile=..\src\WinternetMeter.Windows\app.ico
UninstallDisplayIcon={app}\WinternetMeter.exe
UninstallDisplayName=Winternet Meter
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
; Matches the app's single-instance mutex: setup asks to close a running meter before updating.
AppMutex=WinternetMeter.SingleInstance
CloseApplications=yes
RestartApplications=no

[Tasks]
Name: "startup"; Description: "Start Winternet Meter when I sign in"; GroupDescription: "Options:"
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Options:"; Flags: unchecked

[Files]
Source: "{#SourceExe}"; DestDir: "{app}"; DestName: "WinternetMeter.exe"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\Winternet Meter"; Filename: "{app}\WinternetMeter.exe"
Name: "{autodesktop}\Winternet Meter"; Filename: "{app}\WinternetMeter.exe"; Tasks: desktopicon

[Registry]
; Same value name the app's "Run on startup" menu uses, so both control one entry.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "Winternet Meter"; ValueData: """{app}\WinternetMeter.exe"""; Tasks: startup

[Run]
Filename: "{app}\WinternetMeter.exe"; Description: "Launch Winternet Meter now"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{sys}\taskkill.exe"; Parameters: "/IM WinternetMeter.exe /F"; Flags: runhidden; RunOnceId: "StopMeter"
; Remove the startup entry even if it was turned on from the app's menu (not by this installer).
Filename: "{sys}\reg.exe"; Parameters: "delete ""HKCU\Software\Microsoft\Windows\CurrentVersion\Run"" /v ""Winternet Meter"" /f"; Flags: runhidden; RunOnceId: "RemoveStartup"

; Settings in %AppData%\WinternetMeter are kept on uninstall so a reinstall remembers them.
