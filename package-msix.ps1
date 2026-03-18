# package-msix.ps1 — Assembles an MSIX package from build/publish output
# Used by both Azure Pipelines and local builds.
#
# Prerequisites:
#   - Windows SDK 10.0.22621.0 (for makepri.exe and makeappx.exe)
#   - Morphic app already built and published via msbuild
#
# Usage:
#   .\package-msix.ps1 -Platform x64 -Configuration Release -SourceDir . -OutputMsix build\MorphicSetup-x64.msix

param(
    [Parameter(Mandatory)][ValidateSet("x64","x86","ARM64")][string]$Platform,
    [Parameter(Mandatory)][string]$Configuration,
    [Parameter(Mandatory)][string]$SourceDir,
    [Parameter(Mandatory)][string]$OutputMsix,
    [string]$StagingDir
)

$ErrorActionPreference = "Stop"

# Derive RuntimeIdentifier from Platform
$rid = switch ($Platform) {
    "x64"   { "win-x64" }
    "x86"   { "win-x86" }
    "ARM64" { "win-arm64" }
}

# Derive ProcessorArchitecture for manifest
$procArch = switch ($Platform) {
    "x64"   { "x64" }
    "x86"   { "x86" }
    "ARM64" { "arm64" }
}

$publishDir = "$SourceDir\Morphic\bin\$Platform\$Configuration\net10.0-windows10.0.22621.0\$rid\publish"
$buildDir   = "$SourceDir\Morphic\bin\$Platform\$Configuration\net10.0-windows10.0.22621.0\$rid"
$pkgSrc     = "$SourceDir\Morphic (Package)"
$sdkBin     = "C:\Program Files (x86)\Windows Kits\10\bin\10.0.22621.0\x64"

if (-not $StagingDir) {
    $StagingDir = "$SourceDir\build\MsixStaging-$Platform"
}

# Validate inputs
if (-not (Test-Path "$publishDir\Morphic.exe")) { throw "Publish output not found at $publishDir. Run msbuild /t:publish first." }
if (-not (Test-Path "$buildDir\Morphic.pri"))    { throw "Morphic.pri not found at $buildDir. Run msbuild /t:build first." }
if (-not (Test-Path "$sdkBin\makepri.exe"))      { throw "Windows SDK 10.0.22621.0 not found at $sdkBin." }

# ---- Assemble staging directory ----
Write-Host "Assembling staging directory..." -ForegroundColor Cyan

if (Test-Path $StagingDir) { Remove-Item $StagingDir -Recurse -Force }
New-Item -ItemType Directory -Force -Path $StagingDir | Out-Null

Copy-Item -Path "$publishDir\*" -Destination $StagingDir -Recurse -Force

New-Item -ItemType Directory -Force -Path "$StagingDir\Images" | Out-Null
Copy-Item -Path "$pkgSrc\Images\*" -Destination "$StagingDir\Images\" -Recurse -Force

Copy-Item -Path "$buildDir\Morphic.pri" -Destination "$StagingDir\Morphic.pri" -Force

# ---- Generate AppxManifest.xml ----
Write-Host "Generating AppxManifest.xml..." -ForegroundColor Cyan

[xml]$manifest = Get-Content "$pkgSrc\Package.appxmanifest"
$ns = $manifest.Package.NamespaceURI

Write-Host "  Package version: $($manifest.Package.Identity.Version)"
$manifest.Package.Identity.SetAttribute("ProcessorArchitecture", $procArch)

$appNode = $manifest.Package.Applications.Application
$appNode.Executable = "Morphic.exe"
$appNode.EntryPoint = "Windows.FullTrustApplication"

$resourcesNode = $manifest.Package.Resources
$resourcesNode.RemoveAll()
$resourceElem = $manifest.CreateElement("Resource", $ns)
$resourceElem.SetAttribute("Language", "EN-US")
$resourcesNode.AppendChild($resourceElem) | Out-Null

$depsNode = $manifest.Package.Dependencies
$vclibs = $manifest.CreateElement("PackageDependency", $ns)
$vclibs.SetAttribute("Name", "Microsoft.VCLibs.140.00")
$vclibs.SetAttribute("MinVersion", "14.0.33519.0")
$vclibs.SetAttribute("Publisher", "CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond, S=Washington, C=US")
$depsNode.AppendChild($vclibs) | Out-Null

$vclibsDesktop = $manifest.CreateElement("PackageDependency", $ns)
$vclibsDesktop.SetAttribute("Name", "Microsoft.VCLibs.140.00.UWPDesktop")
$vclibsDesktop.SetAttribute("MinVersion", "14.0.33728.0")
$vclibsDesktop.SetAttribute("Publisher", "CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond, S=Washington, C=US")
$depsNode.AppendChild($vclibsDesktop) | Out-Null

$manifest.Save("$StagingDir\AppxManifest.xml")

# ---- Generate resources.pri ----
Write-Host "Generating resources.pri..." -ForegroundColor Cyan

@(
    "Images\LockScreenLogo.scale-200.png",
    "Images\SplashScreen.scale-200.png",
    "Images\Square150x150Logo.scale-200.png",
    "Images\Square44x44Logo.scale-200.png",
    "Images\Square44x44Logo.targetsize-24_altform-unplated.png",
    "Images\StoreLogo.png",
    "Images\Wide310x150Logo.scale-200.png"
) | Out-File -FilePath "$StagingDir\layout.resfiles" -Encoding utf8

@(
    "$StagingDir\Microsoft.UI.pri",
    "$StagingDir\Microsoft.UI.Xaml.Controls.pri",
    "$StagingDir\Microsoft.WindowsAppRuntime.pri",
    "$StagingDir\Morphic.pri"
) | Out-File -FilePath "$StagingDir\pri.resfiles" -Encoding utf8

$priconfig = @"
<?xml version="1.0" encoding="utf-8"?>
<resources targetOsVersion="10.0.0" majorVersion="1">
  <index root="\" startIndexAt="layout.resfiles">
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
    <indexer-config type="RESFILES" qualifierDelimiter="." />
  </index>
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

& "$sdkBin\makepri.exe" new /pr "$StagingDir" /cf "$StagingDir\priconfig.xml" /mn "$StagingDir\AppxManifest.xml" /of "$StagingDir\resources.pri" /o
if ($LASTEXITCODE -ne 0) { throw "makepri failed with exit code $LASTEXITCODE" }

# Clean up intermediate files
Remove-Item "$StagingDir\priconfig.xml" -Force
Remove-Item "$StagingDir\layout.resfiles" -Force
Remove-Item "$StagingDir\pri.resfiles" -Force
Remove-Item "$StagingDir\Morphic.pri" -Force

# ---- Pack MSIX ----
Write-Host "Packing MSIX..." -ForegroundColor Cyan

$outputDir = Split-Path $OutputMsix -Parent
if ($outputDir -and -not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Force -Path $outputDir | Out-Null
}

& "$sdkBin\makeappx.exe" pack /d "$StagingDir" /p "$OutputMsix" /nv /o
if ($LASTEXITCODE -ne 0) { throw "makeappx failed with exit code $LASTEXITCODE" }

Write-Host "`nMSIX created: $OutputMsix" -ForegroundColor Green
