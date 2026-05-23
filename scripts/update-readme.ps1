# Updates README.md current version + changelog from VERSION and the commit message.
# Called from .githooks/commit-msg (enable: git config core.hooksPath .githooks)

param(
    [Parameter(Mandatory = $true)]
    [string]$CommitMessageFile
)

$root = git rev-parse --show-toplevel 2>$null
if (-not $root) { Write-Error "Not inside a git repository."; exit 1 }
Set-Location $root

$msgPath = $CommitMessageFile
if (-not [System.IO.Path]::IsPathRooted($msgPath)) {
    $msgPath = Join-Path $root $msgPath
}
if (-not (Test-Path $msgPath)) { exit 0 }

$subject = ($lines = Get-Content $msgPath) |
    Where-Object { $_ -and $_ -notmatch '^\s*#' } |
    Select-Object -First 1
if (-not $subject) { exit 0 }
if ($subject -match '^(Merge |fixup!|squash!)') { exit 0 }

$versionLine = (Get-Content (Join-Path $root "VERSION") -Raw).Trim()
if ($versionLine -notmatch '^v') { $versionLine = "v$versionLine" }

$summary = $subject -replace '\|', '/' -replace '\s+', ' '
if ($summary.Length -gt 120) { $summary = $summary.Substring(0, 117) + "..." }

$readmePath = Join-Path $root "README.md"
if (-not (Test-Path $readmePath)) { exit 0 }
$content = Get-Content $readmePath -Raw

$versionDisplay = "**Current version:** ``$versionLine`` (see [``VERSION``](VERSION) and Unity **Player Settings → Version**)."
$content = $content -replace '(?m)^\*\*Current version:\*\*.*$', $versionDisplay

$begin = '<!-- CHANGELOG:BEGIN -->'
$end = '<!-- CHANGELOG:END -->'
$bi = $content.IndexOf($begin)
$ei = $content.IndexOf($end)
if ($bi -lt 0 -or $ei -lt 0 -or $ei -le $bi) {
    Write-Warning "README.md missing CHANGELOG markers; skipping changelog update."
    Set-Content -Path $readmePath -Value $content -NoNewline
    exit 0
}

$before = $content.Substring(0, $bi + $begin.Length)
$block = $content.Substring($bi + $begin.Length, $ei - $bi - $begin.Length)
$after = $content.Substring($ei)

$blockLines = ($block -split "`n" | ForEach-Object { $_.TrimEnd("`r") })
$headerRows = @('| Version | Summary |', '|---------|---------|')
$dataRows = @()
$inTable = $false
foreach ($line in $blockLines) {
    if ($line -match '^\| Version \|') { $inTable = $true; continue }
    if ($line -match '^\|-+\|') { continue }
    if ($inTable -and $line -match '^\|') { $dataRows += $line }
}

$newRow = "| **$versionLine** | $summary |"
$versionPattern = [regex]::Escape($versionLine)
if ($dataRows.Count -gt 0 -and $dataRows[0] -match "\*\*$versionPattern\*\*") {
    $dataRows[0] = $newRow
} else {
    $dataRows = @($newRow) + $dataRows
}

$maxEntries = 30
if ($dataRows.Count -gt $maxEntries) {
    $dataRows = $dataRows[0..($maxEntries - 1)]
}

$newBlock = "`n" + ($headerRows -join "`n") + "`n" + ($dataRows -join "`n") + "`n"
$newContent = $before + $newBlock + $after
Set-Content -Path $readmePath -Value $newContent -NoNewline
Write-Host "README updated for $versionLine"
