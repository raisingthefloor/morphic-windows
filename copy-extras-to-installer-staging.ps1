# copy-build-extras.ps1 — Copies build-output-only files (compiled XAML) to the publish directory
# The publish step does not include .xbf files; they must be copied from the build output.
# Used by both Azure Pipelines and local builds.
#
# Usage:
#   .\copy-build-extras.ps1 -Platform x64 -Configuration Release -SourceDir . -TargetDir MorphicSetup\obj\x64\Release\publish\Morphic

param(
    [Parameter(Mandatory)][ValidateSet("x64","x86","ARM64")][string]$Platform,
    [Parameter(Mandatory)][string]$Configuration,
    [Parameter(Mandatory)][string]$SourceDir,
    [Parameter(Mandatory)][string]$TargetDir
)

$ErrorActionPreference = "Stop"

$rid = switch ($Platform) {
    "x64"   { "win-x64" }
    "x86"   { "win-x86" }
    "ARM64" { "win-arm64" }
}

$buildDir = "$SourceDir\Morphic\bin\$Platform\$Configuration\net10.0-windows10.0.22621.0\$rid"

# Copy compiled XAML files (.xbf) that the publish step does not include
$xbfFiles = Get-ChildItem -Path $buildDir -Filter "*.xbf" -File
if ($xbfFiles.Count -eq 0) {
    throw "No .xbf files found in $buildDir. Ensure the build step completed successfully."
}

foreach ($file in $xbfFiles) {
    Write-Host "Copying $($file.Name)..."
    Copy-Item -Path $file.FullName -Destination "$TargetDir\$($file.Name)" -Force
}

Write-Host "Copied $($xbfFiles.Count) .xbf file(s) to $TargetDir"
