# merge-resources-pri.ps1 -- Merge every per-assembly *.pri in -Dir into a single resources.pri in
# that same directory, using makepri.exe.
#
# WHY: MRT Core's default ResourceLoader (and XAML x:Uid) load "resources.pri" from the app
# directory at runtime, but the WinUI/.NET build only emits the per-assembly PRIs (Morphic.pri,
# Morphic.Controls.pri, ...). The unpackaged MSI publish path merges them in
# finalize-winui-publish.ps1, and the MSIX path in package-msix.ps1; this script is the shared,
# directory-scoped merge so a plain Debug/F5 build can get the same resources.pri without publishing.
#
# KEEP THE priconfig BELOW IN SYNC with finalize-winui-publish.ps1 and package-msix.ps1.
param(
    [Parameter(Mandatory)][string]$Dir,
    [string]$MakePriExe = "C:\Program Files (x86)\Windows Kits\10\bin\10.0.22621.0\x64\makepri.exe"
)

$ErrorActionPreference = "Stop"

# Normalize (the caller may pass a trailing "\." to dodge MSBuild's backslash-before-quote escaping).
$Dir = (Resolve-Path -LiteralPath $Dir).Path.TrimEnd('\')

if (-not (Test-Path $MakePriExe)) { throw "makepri.exe not found at $MakePriExe" }

# Every per-assembly .pri except a resources.pri left over from a previous run.
$priFiles = Get-ChildItem -Path $Dir -Filter *.pri -File |
    Where-Object { $_.Name -ne "resources.pri" } |
    ForEach-Object { $_.FullName }
if ($priFiles.Count -eq 0) { throw "No per-assembly .pri files found in $Dir to merge." }

$priFiles | Out-File -FilePath "$Dir\pri.resfiles" -Encoding utf8

$priconfig = @"
<?xml version="1.0" encoding="utf-8"?>
<resources targetOsVersion="10.0.0" majorVersion="1">
  <index root="\" startIndexAt="pri.resfiles">
    <default>
      <qualifier name="Language" value="en-US" />
      <qualifier name="Contrast" value="standard" />
      <qualifier name="Scale" value="200" />
      <qualifier name="HomeRegion" value="001" />
      <qualifier name="TargetSize" value="256" />
      <qualifier name="LayoutDirection" value="LTR" />
      <qualifier name="DXFeatureLevel" value="DX9" />
      <qualifier name="Configuration" value="" />
      <qualifier name="AlternateForm" value="" />
      <qualifier name="Platform" value="UAP" />
    </default>
    <indexer-config type="PRI" />
    <indexer-config type="RESFILES" qualifierDelimiter="." />
  </index>
</resources>
"@
$priconfig.Trim() | Out-File -FilePath "$Dir\priconfig.xml" -Encoding utf8

& $MakePriExe new /pr "$Dir" /cf "$Dir\priconfig.xml" /of "$Dir\resources.pri" /o
$makePriExitCode = $LASTEXITCODE

Remove-Item "$Dir\priconfig.xml" -Force -ErrorAction SilentlyContinue
Remove-Item "$Dir\pri.resfiles" -Force -ErrorAction SilentlyContinue

if ($makePriExitCode -ne 0) { throw "makepri failed with exit code $makePriExitCode" }
