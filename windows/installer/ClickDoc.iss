; ClickDoc for Windows: per-user installer, no admin rights.
; Build (after windows/scripts/publish.sh or dotnet publish to windows/out/ClickDoc):
;   ISCC.exe /DAppVersion=0.8.0 windows\installer\ClickDoc.iss
; Needs Inno Setup 6.5 or later (ChineseSimplified.isl format).

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif

[Setup]
; Never change AppId: it is how upgrades find the installed copy.
AppId={{A5E94657-17AD-4255-B27D-1D10BA6333F1}
AppName=ClickDoc
AppVersion={#AppVersion}
AppVerName=ClickDoc {#AppVersion}
AppPublisherURL=https://clickdoc.xiaohan.dev
AppSupportURL=https://github.com/ibeeger/clickdoc
DefaultDirName={localappdata}\Programs\ClickDoc
DisableProgramGroupPage=yes
DisableDirPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19041
OutputDir=..\out\installer
OutputBaseFilename=ClickDoc-{#AppVersion}-Setup
SetupIconFile=..\ClickDoc\Assets\ClickDoc.ico
UninstallDisplayIcon={app}\ClickDoc.exe
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no
ShowLanguageDialog=auto

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "chinesesimplified"; MessagesFile: "ChineseSimplified.isl"

[Files]
Source: "..\out\ClickDoc\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[InstallDelete]
; Files from an older version that the new one no longer ships.
Type: filesandordirs; Name: "{app}\*"

[Icons]
Name: "{autoprograms}\ClickDoc"; Filename: "{app}\ClickDoc.exe"

[Run]
Filename: "{app}\ClickDoc.exe"; Description: "{cm:LaunchProgram,ClickDoc}"; Flags: nowait postinstall skipifsilent

; The draft in %LocalAppData%\ClickDoc is user data and survives uninstalling.
