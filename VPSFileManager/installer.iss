; Script do Inno Setup para VPS File Manager
; Instalador SEM permissões de administrador

#define MyAppName "VPS File Manager"
#define MyAppVersion "1.4.1"
#define MyAppPublisher "VPS File Manager"
#define MyAppExeName "VPSFileManager.exe"

[Setup]
; Configurações básicas
AppId={{8F9A3B2C-1D4E-5F6A-7B8C-9D0E1F2A3B4C}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultGroupName={#MyAppName}
OutputDir=.\Installer
OutputBaseFilename=VPSFileManager-Setup
Compression=lzma2/max
SolidCompression=yes

; IMPORTANTE: PrivilegesRequired=lowest para NÃO precisar de admin
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog

; Interface
WizardStyle=modern
SetupIconFile=nuvem.ico
UninstallDisplayIcon={app}\{#MyAppExeName}

; Diretórios customizados para instalação sem admin
UsedUserAreasWarning=no
DefaultDirName={localappdata}\{#MyAppName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "bin\Release\net8.0-windows\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; NOTE: Don't use "Flags: ignoreversion" on any shared system files

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall

[Code]
function InitializeSetup(): Boolean;
begin
  Result := True;
end;
