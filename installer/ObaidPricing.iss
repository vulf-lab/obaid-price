; OBAID Pricing — Inno Setup 6 installer script
; Build: powershell -File installer/build-installer.ps1
;
; Code signing (optional): set environment variables before build:
;   $env:OBAD_SIGN_CERT = "path\to\code-signing.pfx"
;   $env:OBAD_SIGN_PASSWORD = "..."
; Or configure SignTool below once certificates are available.

#define MyAppName "OBAID Pricing"
#define MyAppVersion "1.0.4"
#define MyAppPublisher "OBAID"
#define MyAppURL ""
#define MyAppExeName "ObaidPricing.exe"
#define MyAppId "{{A7E3C91D-4B2F-4E8A-9C1D-6F0E5B8A2D71}"
#define PublishDir "..\artifacts\release\1.0.4"
#define OutputDir "..\artifacts\installer"

[Setup]
AppId={#MyAppId}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppCopyright=Copyright © 2026 OBAID
VersionInfoVersion={#MyAppVersion}.0
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription={#MyAppName} Setup
VersionInfoProductName={#MyAppName}
VersionInfoProductVersion={#MyAppVersion}
DefaultDirName={autopf}\{#MyAppPublisher}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
DisableProgramGroupPage=no
LicenseFile=
InfoBeforeFile=
OutputDir={#OutputDir}
OutputBaseFilename=ObaidPricing-Setup-{#MyAppVersion}
SetupIconFile=..\src\CostWise.App\Assets\obaid-pricing-logo.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
DisableWelcomePage=no
DisableDirPage=no
UsePreviousAppDir=yes
CloseApplications=yes
RestartApplications=no
; Self-contained publish — no .NET Desktop Runtime prerequisite.
; Signed-ready: uncomment SignTool when a code-signing certificate is configured.
; SignTool=ObaidSign
; SignedUninstaller=yes

; Example SignTool definition (uncomment and adjust):
; [SignTool]
; Name: ObaidSign; Command: signtool.exe sign /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 /f $q{$q%OBAD_SIGN_CERT%$q}$q /p $q{$q%OBAD_SIGN_PASSWORD%$q}$q $f

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; Entire Release publish folder (self-contained single-file + any extracted assets such as fonts)
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*.pdb"

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Only remove empty leftover dirs under {app} if any (installed files are tracked by Inno).
Type: dirifempty; Name: "{app}"

[Code]
function InitializeSetup(): Boolean;
begin
  Result := True;
end;
