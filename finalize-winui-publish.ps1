# finalize-winui-publish.ps1 -- Prepares the publish output for the unpackaged MSI installer
#
# SISTER SCRIPT: package-msix.ps1 performs the same .xbf-copy + per-assembly PRI merge
# (steps 1-3 below) inside its larger MSIX packaging flow. If you change the WinUI-resource
# handling here, consider whether the equivalent change is needed there too -- the two
# scripts intentionally mirror each other's resource-staging behavior so the unpackaged
# (MSI) and packaged (MSIX) install paths produce equivalent runtime resource layouts.
#
# What this does (and why):
#   1. Copies Morphic.pri from the BUILD output into the publish folder. msbuild's
#      publish step drops the executable's per-assembly .pri (it keeps the
#      dependency .pri files but expects the executable's PRI to be merged into
#      resources.pri instead -- which it then also doesn't do for unpackaged
#      self-contained apps).
#   2. Copies compiled XAML (.xbf) files from the BUILD output into the publish
#      folder. Same story: publish drops them; they're needed at runtime.
#   3. Runs makepri.exe new to MERGE the per-assembly .pri files (the dependency
#      ones from publish + Morphic.pri we just copied in) into a single
#      resources.pri at the publish-folder root. WinUI's MRM looks for that file
#      at startup; if it's missing, the app crashes inside Microsoft.UI.Xaml.dll
#      with a COM file-not-found cascade (STATUS_STOWED_EXCEPTION).
#
# Prerequisites:
#   - Windows SDK 10.0.22621.0 (for makepri.exe)
#   - Morphic app already built AND published via msbuild
#
# Usage:
#   .\finalize-winui-publish.ps1 -Platform x64 -Configuration Release -SourceDir .
#
# After this runs, the installer should stage from:
#   <SourceDir>\Morphic\bin\<Platform>\<Configuration>\net10.0-windows10.0.22621.0\<rid>\publish\

param(
    [Parameter(Mandatory)][ValidateSet("x64","ARM64")][string]$Platform,
    [Parameter(Mandatory)][string]$Configuration,
    [Parameter(Mandatory)][string]$SourceDir,
    [string]$StagingDir
)

$ErrorActionPreference = "Stop"

# Derive RuntimeIdentifier from Platform
$rid = switch ($Platform) {
    "x64"   { "win-x64" }
    "ARM64" { "win-arm64" }
}

$publishDir = "$SourceDir\Morphic\bin\$Platform\$Configuration\net10.0-windows10.0.22621.0\$rid\publish"
$buildDir   = "$SourceDir\Morphic\bin\$Platform\$Configuration\net10.0-windows10.0.22621.0\$rid"
$sdkBin     = "C:\Program Files (x86)\Windows Kits\10\bin\10.0.22621.0\x64"

# Default to in-place modification of the publish folder (the installer reads from
# there directly). Pass -StagingDir to direct the assembled output elsewhere.
if (-not $StagingDir) {
    $StagingDir = $publishDir
}

# Validate inputs
if (-not (Test-Path "$publishDir\Morphic.exe")) { throw "Publish output not found at $publishDir. Run 'msbuild /t:publish' first." }
if (-not (Test-Path "$buildDir\Morphic.pri"))    { throw "Morphic.pri not found at $buildDir. Run 'msbuild /t:build' first (or include build as part of publish)." }
if (-not (Test-Path "$sdkBin\makepri.exe"))      { throw "Windows SDK 10.0.22621.0 not found at $sdkBin." }

# ---- Stage to separate dir, if requested ----
if ($StagingDir -ne $publishDir) {
    Write-Host "Staging publish output to $StagingDir..." -ForegroundColor Cyan
    if (Test-Path $StagingDir) { Remove-Item $StagingDir -Recurse -Force }
    New-Item -ItemType Directory -Force -Path $StagingDir | Out-Null
    Copy-Item -Path "$publishDir\*" -Destination $StagingDir -Recurse -Force
}

# ---- Copy Morphic.pri from BUILD output ----
# Publish drops the executable's per-assembly PRI (it's expecting the merged
# resources.pri to replace it). We grab it from build and feed it into the merge below.
Write-Host "Copying Morphic.pri from $buildDir..." -ForegroundColor Cyan
Copy-Item -Path "$buildDir\Morphic.pri" -Destination "$StagingDir\Morphic.pri" -Force

# ---- Copy compiled XAML (.xbf) files from BUILD output ----
# Publish drops these too. Without them, XAML pages referenced by the running app
# can't be loaded at runtime.
Write-Host "Copying .xbf files from $buildDir..." -ForegroundColor Cyan
$xbfFiles = Get-ChildItem -Path $buildDir -Filter "*.xbf" -File -Recurse
foreach ($file in $xbfFiles) {
    $relativePath = $file.FullName.Substring($buildDir.Length).TrimStart('\')
    $destPath = Join-Path $StagingDir $relativePath
    $destDir = Split-Path $destPath -Parent
    if (-not (Test-Path $destDir)) {
        New-Item -ItemType Directory -Force -Path $destDir | Out-Null
    }
    Write-Host "  $relativePath"
    Copy-Item -Path $file.FullName -Destination $destPath -Force
}

# ---- Generate resources.pri by MERGING per-assembly .pri files ----
Write-Host "Generating resources.pri..." -ForegroundColor Cyan

# Discover every *.pri in the staging dir except resources.pri itself (in case a
# previous run left one behind). This automatically picks up any future per-assembly
# .pri files the SDK starts shipping (e.g. when WinAppSDK adds new components),
# without needing to update this script.
$priFiles = Get-ChildItem -Path $StagingDir -Filter "*.pri" -File |
    Where-Object { $_.Name -ne "resources.pri" } |
    ForEach-Object { $_.FullName }

if ($priFiles.Count -eq 0) {
    throw "No per-assembly .pri files found in $StagingDir to merge."
}

Write-Host "  Merging $($priFiles.Count) per-assembly .pri files:"
$priFiles | ForEach-Object { Write-Host "    $_" }

$priFiles | Out-File -FilePath "$StagingDir\pri.resfiles" -Encoding utf8

# Minimal priconfig: just the PRI-merge index (no layout.resfiles index because we
# have no MSIX-style image assets to declare).
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
$priconfig.Trim() | Out-File -FilePath "$StagingDir\priconfig.xml" -Encoding utf8

# /pr = project root, /cf = config, /of = output file, /o = overwrite. NO /mn:
# we are NOT producing a package-manifest-aware PRI (this is unpackaged).
& "$sdkBin\makepri.exe" new /pr "$StagingDir" /cf "$StagingDir\priconfig.xml" /of "$StagingDir\resources.pri" /o
if ($LASTEXITCODE -ne 0) { throw "makepri failed with exit code $LASTEXITCODE" }

# Clean up intermediate files
Remove-Item "$StagingDir\priconfig.xml" -Force
Remove-Item "$StagingDir\pri.resfiles" -Force
# Morphic.pri is now merged into resources.pri; remove the standalone copy so the
# installer payload doesn't duplicate it (mirrors what package-msix.ps1 does).
Remove-Item "$StagingDir\Morphic.pri" -Force

Write-Host "`nresources.pri generated at: $StagingDir\resources.pri" -ForegroundColor Green
Write-Host "Installer should stage from: $StagingDir" -ForegroundColor Green
