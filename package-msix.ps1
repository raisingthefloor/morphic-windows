# package-msix.ps1 — Assembles an MSIX package from build/publish output
# Used by both Azure Pipelines and local builds.
#
# SISTER SCRIPT: finalize-winui-publish.ps1 performs the same .xbf-copy + per-assembly PRI
# merge (the resources.pri generation steps below) standalone, for the unpackaged MSI
# installer flow. If you change the WinUI-resource handling here, consider whether the
# equivalent change is needed there too -- the two scripts intentionally mirror each
# other's resource-staging behavior so the packaged (MSIX) and unpackaged (MSI) install
# paths produce equivalent runtime resource layouts.
#
# Prerequisites:
#   - Windows SDK 10.0.22621.0 (for makepri.exe and makeappx.exe)
#   - Morphic app already built and published via msbuild
#
# Usage:
#   .\package-msix.ps1 -Platform x64 -Configuration Release -SourceDir . -OutputMsix build\MorphicSetup-x64.msix
#
# see: https://learn.microsoft.com/en-us/windows/msix/package/manual-packaging-root

param(
    [Parameter(Mandatory)][ValidateSet("x64","ARM64")][string]$Platform,
    [Parameter(Mandatory)][string]$Configuration,
    [Parameter(Mandatory)][string]$SourceDir,
    [Parameter(Mandatory)][string]$OutputMsix,
    [string]$StagingDir
)

$ErrorActionPreference = "Stop"

# Derive RuntimeIdentifier from Platform
$rid = switch ($Platform) {
    "x64"   { "win-x64" }
    "ARM64" { "win-arm64" }
}

# Derive ProcessorArchitecture for manifest
$procArch = switch ($Platform) {
    "x64"   { "x64" }
    "ARM64" { "arm64" }
}

$publishDir = "$SourceDir\Morphic\bin\$Platform\$Configuration\net10.0-windows10.0.22621.0\$rid\publish"
$buildDir   = "$SourceDir\Morphic\bin\$Platform\$Configuration\net10.0-windows10.0.22621.0\$rid"
$pkgSrc     = "$SourceDir\Morphic (Package)"
$sdkBin     = "C:\Program Files (x86)\Windows Kits\10\bin\10.0.22621.0\x64"

if (-not $StagingDir) {
    $StagingDir = "$SourceDir\build\MsixStaging-$Platform"
}

# Validate the SDK and source location up front (these we can't fix ourselves).
if (-not (Test-Path "$sdkBin\makepri.exe"))      { throw "Windows SDK 10.0.22621.0 not found at $sdkBin." }
if (-not (Test-Path "$SourceDir\Morphic\Morphic.csproj")) { throw "Morphic.csproj not found at $SourceDir\Morphic\Morphic.csproj." }

# Ensure a self-contained publish exists. The Morphic.csproj's default does NOT set
# WindowsAppSDKSelfContained=true (it's framework-dependent for both the MSI path with
# chained WindowsAppRuntimeInstall.exe and the WAP / wapproj MSIX path which uses
# WindowsAppSdkBootstrapInitialize=false to suppress bootstrap auto-init). This script,
# however, builds a SELF-CONTAINED MSIX with the .pri merge below, so it needs the
# publish output to include Microsoft.WindowsAppRuntime.pri, Microsoft.UI.pri,
# Microsoft.UI.Xaml.Controls.pri, and the matching WinAppSDK DLLs.
#
# We pass -p:MorphicBuildSelfContained=true rather than -p:WindowsAppSDKSelfContained=true.
# Morphic.csproj has a conditional that translates the Morphic-specific property into
# WindowsAppSDKSelfContained=true (see Morphic\Morphic.csproj). The custom property name
# is required because WindowsAppSDKSelfContained, when set globally on the dotnet publish
# command line, propagates to library ProjectReferences (Morphic.Controls, etc.) and the
# WindowsAppSDK targets there reject it with "WindowsAppSDKSelfContained should not be
# applied to a class library." Routing through MorphicBuildSelfContained keeps the global
# property name unrecognized by the library projects while still triggering the right
# behavior in the app project.
#
# Skips the publish if Morphic.exe is already present in the publish dir AND the
# framework .pri file is there (lets local devs re-run the script repeatedly without
# re-publishing every time).
$skipPublish = (Test-Path "$publishDir\Morphic.exe") -and (Test-Path "$publishDir\Microsoft.WindowsAppRuntime.pri")
if (-not $skipPublish) {
    Write-Host "Publishing Morphic.csproj (Configuration=$Configuration Platform=$Platform RuntimeIdentifier=$rid self-contained) ..." -ForegroundColor Cyan
    # -r $rid is required: self-contained publishes (WindowsAppSDKSelfContained=true via
    # our MorphicBuildSelfContained translation) need an explicit RuntimeIdentifier so
    # the publish knows which native CoreCLR / WinAppSDK arch to bundle. Without it, .NET
    # SDK picks the first entry from Morphic.csproj's <RuntimeIdentifiers>win-x64;win-arm64</...>
    # which is win-x64 -- and on an ARM64 build that mismatches PlatformTarget=arm64 with
    # NETSDK1032 "The RuntimeIdentifier platform 'win-x64' and the PlatformTarget 'arm64'
    # must be compatible." Passing -r $rid (win-x64 or win-arm64, derived from $Platform
    # above) keeps the two in sync.
    & dotnet publish "$SourceDir\Morphic\Morphic.csproj" -c $Configuration -p:Platform=$Platform -r $rid -p:MorphicBuildSelfContained=true --nologo
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE" }
}

# Post-publish sanity checks: the publish step should have produced the things we'll
# consume in the staging assembly below. If these still fail, something more unusual
# has happened than just a stale publish.
if (-not (Test-Path "$publishDir\Morphic.exe")) { throw "Publish output not found at $publishDir after publish step." }
if (-not (Test-Path "$buildDir\Morphic.pri"))    { throw "Morphic.pri not found at $buildDir after publish step." }
if (-not (Test-Path "$publishDir\Microsoft.WindowsAppRuntime.pri")) { throw "Microsoft.WindowsAppRuntime.pri not found in publish output; WindowsAppSDKSelfContained=true should have deployed it." }

# ---- Assemble staging directory ----
Write-Host "Assembling staging directory..." -ForegroundColor Cyan

if (Test-Path $StagingDir) { Remove-Item $StagingDir -Recurse -Force }
New-Item -ItemType Directory -Force -Path $StagingDir | Out-Null

Copy-Item -Path "$publishDir\*" -Destination $StagingDir -Recurse -Force

New-Item -ItemType Directory -Force -Path "$StagingDir\Images" | Out-Null
Copy-Item -Path "$pkgSrc\Images\*" -Destination "$StagingDir\Images\" -Recurse -Force

Copy-Item -Path "$buildDir\Morphic.pri" -Destination "$StagingDir\Morphic.pri" -Force

# Copy compiled XAML files (.xbf) from the build output (they are not included in the publish output)
$xbfFiles = Get-ChildItem -Path $buildDir -Filter "*.xbf" -File -Recurse
foreach ($file in $xbfFiles) {
    $relativePath = $file.FullName.Substring($buildDir.Length).TrimStart('\')
    $destPath = Join-Path $StagingDir $relativePath
    $destDir = Split-Path $destPath -Parent
    if (-not (Test-Path $destDir)) {
        New-Item -ItemType Directory -Force -Path $destDir | Out-Null
    }
    Write-Host "Copying $relativePath..."
    Copy-Item -Path $file.FullName -Destination $destPath -Force
}

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

# Reset Dependencies' PackageDependency children. The wapproj template
# (Package.appxmanifest) declares a Microsoft.WindowsAppRuntime.2 framework dep for the
# wapproj's framework-dependent MSIX flow, but THIS script builds a SELF-CONTAINED MSIX
# (all WinAppSDK DLLs deployed app-locally via the publish step above). Carrying the
# framework dep into a self-contained MSIX produces duplicate copies of WinAppSDK at
# runtime (one local, one from the framework package the OS would install to satisfy
# the dep) -- the same root cause as the CoreMessagingXP fail-fast we hit during wapproj
# F5 testing. Stripping the existing PackageDependency entries here and re-adding only
# the VCLibs deps below keeps the script-built MSIX purely self-contained, matching the
# pre-PackageDependency-addition behavior of the historically-working pipeline build.
# TargetDeviceFamily nodes inside Dependencies are unaffected.
$existingPackageDeps = @($depsNode.ChildNodes | Where-Object { $_.LocalName -eq 'PackageDependency' })
foreach ($oldDep in $existingPackageDeps) {
    $depsNode.RemoveChild($oldDep) | Out-Null
}

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
