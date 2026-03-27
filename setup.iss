[Setup]
AppName=Pusula
AppVersion=1.1.3
AppPublisher=Aras
DefaultDirName={autopf}\Pusula
DefaultGroupName=Pusula
UninstallDisplayIcon={app}\Pusula.exe
OutputDir=SetupOutput
OutputBaseFilename=PusulaSetup
SetupIconFile=Resources\app.ico
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
DisableProgramGroupPage=yes
DisableDirPage=yes
PrivilegesRequired=admin

[Languages]
Name: "turkish"; MessagesFile: "compiler:Languages\Turkish.isl"

[Messages]
turkish.WelcomeLabel2=Pusula v1.1.3 yazilimini bilgisayariniza kuracaktir.%n%nKurulum Program Files klasorune yapilacak ve masaustune kisayol olusturulacaktir.%n%nDevam etmek icin 'Ileri' dügmesine tiklayin.

[Files]
; Build çıktısından uygulama dosyaları (publish değil, doğrudan build output)
Source: "bin\Release\net8.0-windows\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion
Source: "bin\Release\net8.0-windows\win-x64\data\*"; DestDir: "{app}\data"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "bin\Release\net8.0-windows\win-x64\LatoFont\*"; DestDir: "{app}\LatoFont"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "bin\Release\net8.0-windows\win-x64\runtimes\*"; DestDir: "{app}\runtimes"; Flags: ignoreversion recursesubdirs createallsubdirs
; Source: "bin\Release\net8.0-windows\win-x64\tr\*"; DestDir: "{app}\tr"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autodesktop}\Pusula"; Filename: "{app}\Pusula.exe"; IconFilename: "{app}\Pusula.exe"
Name: "{group}\Pusula"; Filename: "{app}\Pusula.exe"; IconFilename: "{app}\Pusula.exe"
Name: "{group}\Pusula Kaldir"; Filename: "{uninstallexe}"

[Run]
Filename: "{app}\Pusula.exe"; Description: "Pusula programini baslat"; Flags: nowait postinstall skipifsilent

[Code]
procedure HideRuntimeFiles();
var
  AppDir: String;
  ResultCode: Integer;
begin
  AppDir := ExpandConstant('{app}');
  { Hide all DLLs, config files, PDB files }
  Exec('cmd.exe', '/c attrib +h "' + AppDir + '\*.dll"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Exec('cmd.exe', '/c attrib +h "' + AppDir + '\*.json"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Exec('cmd.exe', '/c attrib +h "' + AppDir + '\*.pdb"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  { Hide runtime/font/localization subfolders }
  Exec('cmd.exe', '/c attrib +h "' + AppDir + '\runtimes"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Exec('cmd.exe', '/c attrib +h "' + AppDir + '\LatoFont"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Exec('cmd.exe', '/c attrib +h "' + AppDir + '\tr"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

procedure GrantDataFolderWriteAccess();
var
  DataDir: String;
  ResultCode: Integer;
begin
  DataDir := ExpandConstant('{app}\data');
  { Grant Users group full control on data folder so the app can create/copy databases without admin }
  Exec('icacls.exe', '"' + DataDir + '" /grant *S-1-5-32-545:(OI)(CI)F /T /Q', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    HideRuntimeFiles();
    GrantDataFolderWriteAccess();
  end;
end;
