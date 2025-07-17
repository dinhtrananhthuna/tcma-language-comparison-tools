; Inno Setup Script for TCMA Language Comparison Tool
; Requires Inno Setup: https://jrsoftware.org/isinfo.php

#define MyAppName "TCMA Language Comparison Tool"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "TCMA Team"
#define MyAppExeName "Tcma.LanguageComparison.Gui.exe"
#define MyAppURL "https://github.com/your-repo/tcma-language-comparison-tools"

[Setup]
AppId={{A8F4C3E7-2B91-4D56-8F73-1E4A6C9B2D8F}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
LicenseFile=LICENSE.txt
PrivilegesRequired=lowest
OutputDir=installer-output
OutputBaseFilename=TCMA-LanguageComparison-Setup-{#MyAppVersion}
SetupIconFile=app-icon.ico
Compression=lzma
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "quicklaunchicon"; Description: "{cm:CreateQuickLaunchIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked; OnlyBelowVersion: 6.1

[Files]
; Main application files
Source: "publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; Sample files (optional)
Source: "..\..\sample\*.csv"; DestDir: "{app}\sample"; Flags: ignoreversion; Check: SampleFilesExist

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon
Name: "{userappdata}\Microsoft\Internet Explorer\Quick Launch\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: quicklaunchicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Registry]
; Register file associations (optional)
Root: HKCU; Subkey: "Software\Classes\.tcma"; ValueType: string; ValueName: ""; ValueData: "TCMAProject"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\TCMAProject"; ValueType: string; ValueName: ""; ValueData: "TCMA Language Comparison Project"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\TCMAProject\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\{#MyAppExeName},0"
Root: HKCU; Subkey: "Software\Classes\TCMAProject\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""

[Code]
function SampleFilesExist: Boolean;
begin
  Result := DirExists(ExpandConstant('{src}\..\..\sample'));
end;

procedure InitializeWizard;
begin
  WizardForm.LicenseAcceptedRadio.Checked := True;
end;