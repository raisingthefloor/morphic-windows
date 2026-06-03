# stamp-string-ids.ps1 -- Ensure every <data> entry in the app's en-US Resources.resw carries a
# STABLE loc-id in its <comment>, e.g. "[loc-id:3f8a1c9e7b2d4f60]".
#
# WHY: the morphic-localization "extract-up" step matches each product string to its catalog entry
# (and its translations) by this loc-id, NOT by the resource key name. The id is assigned ONCE and
# never changes, and it lives INSIDE the string's <comment> -- so renaming the resource key (the
# name= attribute) or editing the English keeps the SAME loc-id. That turns a key rename into a
# tracked rename (the translations follow automatically) instead of an orphan + brand-new string that
# would need manual review. The loc-id sits in the <comment>, which the WinUI/PRI build and the
# Morphic.Localization.Strings source generator both ignore, so stamping never changes the built app.
#
# Idempotent: entries that already have a loc-id are left untouched. Run it after adding new strings
# (and a CI check can fail the build if any entry is missing one).
param(
    [string]$ReswPath = (Join-Path $PSScriptRoot "Morphic\Strings\en-US\Resources.resw"),
    [switch]$CheckOnly   # exit 1 if any entry lacks a loc-id; do not modify the file
)

$ErrorActionPreference = "Stop"
if (-not (Test-Path -LiteralPath $ReswPath)) { throw "resw not found: $ReswPath" }

$content = Get-Content -LiteralPath $ReswPath -Raw
$newLine = if ($content -match "`r`n") { "`r`n" } else { "`n" }
$script:stampedCount = 0
$script:missingCount = 0

$evaluator = [System.Text.RegularExpressions.MatchEvaluator] {
    param($match)
    $block = $match.Value
    if ($block -match '\[loc-id:[0-9a-f]+\]') { return $block }   # already stamped
    $script:missingCount++
    if ($CheckOnly) { return $block }

    $id = ([guid]::NewGuid().ToString('N')).Substring(0, 16)
    $script:stampedCount++

    $commentMatch = [regex]::Match($block, '(?s)<comment>(.*?)</comment>')
    if ($commentMatch.Success) {
        # Prepend the id to the existing translator note.
        $note = $commentMatch.Groups[1].Value
        $newComment = "<comment>[loc-id:$id] $note</comment>"
        return $block.Substring(0, $commentMatch.Index) + $newComment + $block.Substring($commentMatch.Index + $commentMatch.Length)
    }

    # No <comment> yet: insert one (id only) right before the closing </data>, matching indentation.
    $closeMatch = [regex]::Match($block, '(?m)^(?<indent>[ \t]*)</data>[ \t]*$')
    if ($closeMatch.Success) {
        $indent = $closeMatch.Groups['indent'].Value
        $insert = $indent + "  <comment>[loc-id:$id]</comment>" + $newLine
        return $block.Substring(0, $closeMatch.Index) + $insert + $block.Substring($closeMatch.Index)
    }
    return $block
}

$updated = [System.Text.RegularExpressions.Regex]::Replace($content, '(?s)<data\b[^>]*>.*?</data>', $evaluator)

if ($CheckOnly) {
    if ($script:missingCount -gt 0) {
        Write-Host "FAIL: $($script:missingCount) en-US string(s) are missing a loc-id. Run stamp-string-ids.ps1." -ForegroundColor Red
        exit 1
    }
    Write-Host "OK: every en-US string has a loc-id."
    exit 0
}

if ($script:stampedCount -gt 0) {
    [System.IO.File]::WriteAllText($ReswPath, $updated, (New-Object System.Text.UTF8Encoding($true)))
}
Write-Host "Stamped $($script:stampedCount) new loc-id(s) in $ReswPath"
