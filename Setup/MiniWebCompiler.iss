#define RevFileName "..\MiniWebCompiler\bin\Release\MiniWebCompiler.exe"
#define RevId GetStringFileInfo(RevFileName, "ProductVersion")
#define ShortRevId GetFileVersion(RevFileName)

[Setup]
AppName=Mini Web Compiler
AppVersion={#RevId}
OutputBaseFilename=MiniWebCompiler-Setup-{#RevId}
AppId={{3273DC1C-187B-48C5-9B98-34B733E4D74C}
ShowLanguageDialog=no
UsePreviousGroup=False
DisableProgramGroupPage=yes
OutputDir=.
ArchitecturesInstallIn64BitMode=x64
ArchitecturesAllowed=x64
SolidCompression=True
MinVersion=0,6.1
DisableWelcomePage=True
DefaultDirName={localappdata}\MiniWebCompiler
AllowUNCPath=False
UsePreviousAppDir=False
PrivilegesRequired=lowest
DisableDirPage=yes
DisableReadyPage=True
UninstallDisplayName=Mini Web Compiler
UninstallDisplayIcon={app}\MiniWebCompiler.exe
AppPublisher=Yves Goergen
AppPublisherURL=https://unclassified.software/apps/miniwebcompiler
VersionInfoVersion={#ShortRevId}
VersionInfoProductName=Mini Web Compiler
VersionInfoProductVersion={#ShortRevId}
InternalCompressLevel=max

; Setup design
; Large image max. 164x314 pixels, small image max. 55x58 pixels
WizardImageStretch=no
WizardSmallImageFile=MiniWebCompiler_48.bmp

[Tasks]
Name: startupicon; Description: "Start Mini Web Compiler with Windows"

[InstallDelete]
; First clean up any old files
Type: filesandordirs; Name: "{app}\node_modules"

[Files]
Source: "..\MiniWebCompiler\bin\Release\*"; DestDir: "{app}"; Flags: ignoreversion
Source: "LICENSE-3RD-PARTY.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\LICENSE"; DestDir: "{app}"; DestName: "LICENSE.txt"; Flags: ignoreversion

; Node.js
Source: "C:\Program Files\nodejs\node.exe"; DestDir: "{app}"; Flags: ignoreversion

; Node modules
#define AppData GetEnv("AppData")

Source: "{#AppData}\npm\babel.cmd"; DestDir: "{app}"; Flags: ignoreversion createallsubdirs recursesubdirs
Source: "{#AppData}\npm\node_modules\babel-cli\*"; DestDir: "{app}\node_modules\babel-cli"; Flags: ignoreversion createallsubdirs recursesubdirs
Source: "{#AppData}\npm\node_modules\babel-preset-env\*"; DestDir: "{app}\node_modules\babel-preset-env"; Flags: ignoreversion createallsubdirs recursesubdirs
;Source: "{#AppData}\npm\node_modules\babel-preset-minify\*"; DestDir: "{app}\node_modules\babel-preset-minify"; Flags: ignoreversion createallsubdirs recursesubdirs
;(Add to LICENSE-3RD-PARTY.txt if used)

Source: "{#AppData}\npm\csso.cmd"; DestDir: "{app}"; Flags: ignoreversion createallsubdirs recursesubdirs
Source: "{#AppData}\npm\node_modules\csso-cli\*"; DestDir: "{app}\node_modules\csso-cli"; Flags: ignoreversion createallsubdirs recursesubdirs

Source: "{#AppData}\npm\rollup.cmd"; DestDir: "{app}"; Flags: ignoreversion createallsubdirs recursesubdirs
Source: "{#AppData}\npm\node_modules\rollup\*"; DestDir: "{app}\node_modules\rollup"; Flags: ignoreversion createallsubdirs recursesubdirs

Source: "{#AppData}\npm\uglifyjs.cmd"; DestDir: "{app}"; Flags: ignoreversion createallsubdirs recursesubdirs
Source: "{#AppData}\npm\node_modules\uglify-js\*"; DestDir: "{app}\node_modules\uglify-js"; Flags: ignoreversion createallsubdirs recursesubdirs

Source: "{#AppData}\npm\sass.cmd"; DestDir: "{app}"; Flags: ignoreversion createallsubdirs recursesubdirs
Source: "{#AppData}\npm\node_modules\sass\*"; DestDir: "{app}\node_modules\sass"; Flags: ignoreversion createallsubdirs recursesubdirs

[Icons]
Name: "{userprograms}\Mini Web Compiler"; Filename: "{app}\MiniWebCompiler.exe"; WorkingDir: "{app}"; IconFilename: "{app}\MiniWebCompiler.exe"; IconIndex: 0
Name: "{userstartup}\Mini Web Compiler"; Filename: "{app}\MiniWebCompiler.exe"; Parameters: "/hide"; WorkingDir: "{app}"; IconFilename: "{app}\MiniWebCompiler.exe"; IconIndex: 0; Tasks: startupicon

[Run]
Filename: "{app}\MiniWebCompiler.exe"; WorkingDir: "{app}"; Flags: postinstall skipifsilent nowait; Description: "Start Mini Web Compiler now"

[Code]
procedure CloseMiniWebCompiler();
	var ResultCode: Integer;
begin
	// Terminate Mini Web Compiler if it's running
	if (FileExists(ExpandConstant('{app}\MiniWebCompiler.exe'))) then
	begin
		Exec(ExpandConstant('{app}\MiniWebCompiler.exe'), '/exit', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
	end;
end;

procedure CurPageChanged(CurPageID: Integer);
begin
	if CurPageID = wpSelectTasks then
	begin
		// Change "Next" button to "Install" on the first page, because it won't ask any more
		WizardForm.NextButton.Caption := 'Install';
	end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
	CloseMiniWebCompiler();
end;

procedure InitializeUninstallProgressForm();
begin
	CloseMiniWebCompiler();
end;
