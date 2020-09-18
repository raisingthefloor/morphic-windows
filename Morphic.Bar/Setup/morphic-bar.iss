#define AppName "Morphic Community Bar"
#define WebSite "https://morphic.org/"
#define Company "Raising the Floor"
#define MainExe "Morphic.Bar.exe"
#define BuildPath ".\build" 
;#define AppVersion GetFileVersion("{#BuildPath}\{#MainExe}")
#define AppVersion GetDateTimeString('yyyy.mm.dd', '', '')
#define RegPath "Software\Morphic\Bar"


[Setup]
AppId={{3F697D51-D879-4C2E-A0DB-56AA3370597B}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}

AppPublisher={#Company}
AppPublisherURL={#WebSite}
AppSupportURL={#WebSite}
AppUpdatesURL={#WebSite}

DefaultDirName={pf}\Morphic.Bar
DefaultGroupName={#AppName}

LicenseFile=.\LICENSE.txt

Compression=lzma
SolidCompression=yes

OutputDir=.\output
OutputBaseFilename=MorphicBar-{#AppVersion}

WizardStyle=modern

DisableWelcomePage=False
DisableDirPage=yes
DisableReadyPage=True
DisableReadyMemo=True
DisableProgramGroupPage=yes

[Files]
Source: "{#BuildPath}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#MainExe}"
Name: "{commondesktop}\{#AppName}"; Filename: "{app}\{#MainExe}"

[Run]
Filename: "{app}\{#MainExe}"; Description: "{cm:LaunchProgram,{#StringChange(AppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Registry]
Root: "HKLM"; Subkey: {#RegPath}; ValueType: string; ValueName: "InstallerPath"; ValueData: "{srcexe}"
Root: "HKLM"; Subkey: {#RegPath}; ValueType: string; ValueName: "UnunstallerPath"; ValueData: "{uninstallexe}"
Root: "HKLM"; Subkey: {#RegPath}; ValueType: string; ValueName: "InstallPath"; ValueData: "{app}"
