; RayLite installer — Inno Setup 6
; Build:  1) dotnet publish ... -o publish   (see README.md)
;         2) ISCC.exe setup.iss              ->  installer\RayLite-Setup.exe

#define AppName      "RayLite"
#define AppVersion   "1.0.0"
#define AppPublisher "RayLite Contributors"
#define AppURL       "https://github.com/your-username/RayLite"
#define AppExe       "RayLite.exe"

[Setup]
AppId={{9F3B6C1E-4A27-4D8E-8B53-2C7A1E5D6F40}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}/issues
AppUpdatesURL={#AppURL}/releases
VersionInfoVersion={#AppVersion}
DefaultDirName={autopf}\{#AppName}
DisableProgramGroupPage=yes
DefaultGroupName={#AppName}
LicenseFile=LICENSE
SetupIconFile=src\RayLite\app.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
OutputDir=installer
OutputBaseFilename=RayLite-Setup
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
MinVersion=10.0
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; Per-user install by default (no admin prompt); the user can choose "all users" in the dialog.
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
; Same name as the Mutex in App.xaml.cs: setup/uninstall ask to close a running instance.
AppMutex=RayLite_SingleInstance_Mutex
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startup"; Description: "Launch {#AppName} on startup"; GroupDescription: "Startup:"; Flags: unchecked

[Files]
Source: "publish\{#AppExe}"; DestDir: "{app}"; Flags: ignoreversion
Source: "LICENSE"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Registry]
; The app itself manages this value afterwards (Settings > Run on startup).
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "{#AppName}"; ValueData: """{app}\{#AppExe}"""; Flags: uninsdeletevalue; Tasks: startup

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

[Code]
procedure CurrentUninstallStepChanged(CurStep: TUninstallStep);
begin
  if CurStep = usPostUninstall then
  begin
    { Remove the startup entry even if it was enabled later from the app's Settings window. }
    RegDeleteValue(HKEY_CURRENT_USER, 'Software\Microsoft\Windows\CurrentVersion\Run', '{#AppName}');

    { Offer to remove user settings and the index/error logs. }
    if not UninstallSilent then
    begin
      if MsgBox('Do you also want to remove your RayLite settings?', mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES then
        DelTree(ExpandConstant('{userappdata}\{#AppName}'), True, True, True);
    end;
  end;
end;
