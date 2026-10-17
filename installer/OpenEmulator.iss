#ifndef BuildDir
  #define BuildDir "..\bin"
#endif
#ifndef DistDir
  #define DistDir "..\dist"
#endif
#define AppVersion GetStringFileInfo(BuildDir + "\OpenEmulator.exe", "ProductVersion")

[Setup]
AppId={{BFF83B93-9117-47F8-AAD2-8A440F655BD4}
AppName=Open Emulator
AppVersion={#AppVersion}
AppPublisher=srviniaviz
AppPublisherURL=https://github.com/srviniaviz/open-emulator
AppSupportURL=https://github.com/srviniaviz/open-emulator/issues
AppUpdatesURL=https://github.com/srviniaviz/open-emulator/releases
DefaultDirName={localappdata}\Programs\Open Emulator
DefaultGroupName=Open Emulator
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
MinVersion=10.0
OutputDir={#DistDir}
OutputBaseFilename=OpenEmulator-{#AppVersion}-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
SetupIconFile=..\assets\OpenEmulator.ico
UninstallDisplayIcon={app}\OpenEmulator.exe
CloseApplications=yes
RestartApplications=no
SetupLogging=yes

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#BuildDir}\OpenEmulator.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#BuildDir}\OpenEmulator.exe.config"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\README.md"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{userprograms}\Open Emulator"; Filename: "{app}\OpenEmulator.exe"
Name: "{userdesktop}\Open Emulator"; Filename: "{app}\OpenEmulator.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\OpenEmulator.exe"; Description: "{cm:LaunchProgram,Open Emulator}"; Flags: nowait postinstall skipifsilent

[Code]
function InitializeSetup: Boolean;
begin
  Result := IsDotNetInstalled(net48, 0);
  if not Result then
    SuppressibleMsgBox('Open Emulator precisa do .NET Framework 4.8 ou superior. Instale pelo Windows Update e tente novamente.', mbCriticalError, MB_OK, IDOK);
end;
