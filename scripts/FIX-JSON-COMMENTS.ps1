# =============================================================================
# ERP-SYSTEM v1.0.17 - FIX JSON COMMENTS
# =============================================================================
# يكتشف ويزيل أي SQL-style comments (-- ) داخل data-types/*.json files.
# C# JSON parser بيدعم فقط // و /* */ ، مش --.
#
# Usage:
#   powershell -File scripts\FIX-JSON-COMMENTS.ps1
# =============================================================================

[CmdletBinding()]
param()

$ErrorActionPreference = "Continue"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RootDir   = Resolve-Path "$ScriptDir\.."
$JsonDir   = Join-Path $RootDir "src\backend\Host\data-types"

function W($c, $m) { Write-Host $m -ForegroundColor $c }

W Cyan "============================================================"
W Cyan "  ERP-SYSTEM v1.0.17 - FIX JSON COMMENTS"
W Cyan "============================================================"
W Cyan ""

$fixed = 0
$errors = 0

foreach ($file in Get-ChildItem -Path $JsonDir -Filter "*.json") {
    $content = Get-Content $file.FullName -Raw
    $original = $content

    # Strip " -- anything to end-of-line" patterns (SQL inline comments inside JSON values)
    # Pattern: " -- (followed by anything until newline)
    $content = $content -replace '"(\s*)--[^\n\r]*(\r?\n|$)', '",$1$2'

    # Strip line-start "  -- comment" (entire line)
    $content = $content -replace '(?m)^\s*--[^\n]*\r?\n', ''

    if ($content -ne $original) {
        $content | Set-Content $file.FullName -NoNewline -Encoding UTF8
        W Green "[+] Fixed: $($file.Name)"
        $fixed++
    }
}

W Cyan ""
W Green "============================================================"
W Green "  DONE - fixed $fixed files"
W Green "============================================================"
W Green ""
W Yellow "Now re-run:"
W Yellow "  powershell -File scripts\INSTALL-ULTIMATE.ps1 -Reset"
W Cyan ""
