; MicLink Windows installer (Inno Setup 6)
; Build: iscc Pc\installer\MicLinkSetup.iss
; Requires: Release publish folder + signed driver in Assets\Driver

#define AppName "MicLink"
#define AppVersion "0.1.0"
#define AppPublisher "MicLink"
#define PublishDir "..\MicLinkWinUI\MicLinkWinUI\bin\Release\net8.0-windows10.0.19041.0\win-x64\publish"

[Setup]
AppId={{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
OutputDir=output
OutputBaseFilename=MicLinkSetup-{#AppVersion}-x64
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
WizardStyle=modern
UninstallDisplayIcon={app}\MicLinkWinUI.exe

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"

[Tasks]
Name: "desktopicon"; Description: "Ярлык на рабочем столе"; GroupDescription: "Дополнительно:"

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\MicLinkWinUI.exe"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\MicLinkWinUI.exe"; Tasks: desktopicon

[Run]
Filename: "powershell.exe"; \
  Parameters: "-ExecutionPolicy Bypass -NoProfile -File ""{app}\Assets\Scripts\install-driver.ps1"""; \
  StatusMsg: "Установка драйвера MicLink Microphone…"; \
  Flags: runhidden waituntilterminated; \
  Description: "Установить виртуальный микрофон"

[UninstallRun]
Filename: "pnputil.exe"; Parameters: "/delete-driver oem*.inf /uninstall /force"; Flags: runhidden; \
  Description: "Удалить драйвер (если установлен через MicLink)"

[Code]
function InitializeSetup(): Boolean;
begin
  Result := True;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    MsgBox('MicLink установлен.' + #13#10 + #13#10 +
      '1. Запустите MicLink с ярлыка' + #13#10 +
      '2. Подключите телефон (PIN на экране ПК)' + #13#10 +
      '3. В Discord выберите микрофон «MicLink Virtual Audio»' + #13#10 + #13#10 +
      'Если драйвер не установился — запустите MicLink от администратора и нажмите «Установить драйвер» в настройках.',
      mbInformation, MB_OK);
  end;
end;
