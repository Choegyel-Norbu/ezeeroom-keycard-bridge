; Inno Setup script for ezeeroom-keycard-bridge (guide §1.1: "ships as an installer:
; installs as auto-start Windows service + writes config").
;
; Build on Windows:
;   1. ..\build\publish.ps1              (produces ..\publish\win-x86)
;   2. iscc EzeeroomKeycardBridge.iss    (Inno Setup 6+)
;
; After install, provision the shared token once from an admin shell:
;   "C:\Program Files (x86)\Ezeeroom Keycard Bridge\EzeeroomKeycardBridge.exe" set-token <token>
; then edit appsettings.json (AllowedOrigin, Api.BaseUrl, EncoderMode) and restart the service.

#define AppName "Ezeeroom Keycard Bridge"
#define AppVersion "0.1.0"
#define ServiceName "EzeeroomKeycardBridge"
#define ExeName "EzeeroomKeycardBridge.exe"

[Setup]
AppId={{7E1B7C1E-2A54-4A6B-9F1D-EZ0KEYCARD01}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=DCPL
DefaultDirName={autopf32}\{#AppName}
DisableProgramGroupPage=yes
OutputBaseFilename=ezeeroom-keycard-bridge-{#AppVersion}-setup
OutputDir=output
Compression=lzma2
SolidCompression=yes
; x86 service, but installable on 64-bit Windows (installs under Program Files (x86))
ArchitecturesAllowed=x86 x64
PrivilegesRequired=admin

[Files]
Source: "..\publish\win-x86\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion

[Dirs]
; Journal + DPAPI token blob live here. TODO: tighten ACL to the service account + admins.
Name: "{commonappdata}\{#ServiceName}"

[Run]
; Register as auto-start Windows service and start it.
Filename: "{sys}\sc.exe"; Parameters: "create {#ServiceName} binPath= ""{app}\{#ExeName}"" start= auto DisplayName= ""{#AppName}"""; Flags: runhidden
Filename: "{sys}\sc.exe"; Parameters: "description {#ServiceName} ""Local bridge between ezeeroom-web and the ProUSB key-card encoder."""; Flags: runhidden
Filename: "{sys}\sc.exe"; Parameters: "start {#ServiceName}"; Flags: runhidden

[UninstallRun]
Filename: "{sys}\sc.exe"; Parameters: "stop {#ServiceName}"; Flags: runhidden; RunOnceId: "StopService"
Filename: "{sys}\sc.exe"; Parameters: "delete {#ServiceName}"; Flags: runhidden; RunOnceId: "DeleteService"
