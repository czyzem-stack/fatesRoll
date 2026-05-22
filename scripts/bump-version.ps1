# Bumps FatesRoll version in VERSION + ProjectSettings/ProjectSettings.asset
# Default: increment patch (0.0.001 -> 0.0.002)
# -Sync: set patch from commits since d546f80c (v0.0.001 baseline)

param([switch]$Sync)

$root = git rev-parse --show-toplevel 2>$null
if (-not $root) { Write-Error "Not inside a git repository."; exit 1 }
Set-Location $root

$versionFile = Join-Path $root "VERSION"
$settingsFile = Join-Path $root "ProjectSettings/ProjectSettings.asset"
$baseline = "d546f80c"

function Get-PatchNumber {
    $line = (Get-Content $versionFile -Raw).Trim()
    if ($line -match 'v?0\.0\.(\d+)') { return [int]$matches[1] }
    return 1
}

function Set-VersionFiles([int]$patch) {
    $semver = "0.0.{0:D3}" -f $patch
    $tag = "v$semver"
    Set-Content -Path $versionFile -Value $tag -NoNewline
    $content = Get-Content $settingsFile -Raw
    $content = $content -replace 'bundleVersion: \d+\.\d+\.\d+', "bundleVersion: $semver"
    $content = $content -replace 'visionOSBundleVersion: \d+\.\d+\.\d+', "visionOSBundleVersion: $semver"
    $content = $content -replace 'tvOSBundleVersion: \d+\.\d+\.\d+', "visionOSBundleVersion: $semver"
    $content = $content -replace 'switchDisplayVersion: \d+\.\d+\.\d+', "switchDisplayVersion: $semver"
    $content = $content -replace 'AndroidBundleVersionCode: \d+', "AndroidBundleVersionCode: $patch"
    Set-Content -Path $settingsFile -Value $content -NoNewline
    Write-Host "Version set to $tag"
}

if ($Sync) {
    $afterBaseline = [int](git rev-list --count "${baseline}..HEAD")
    Set-VersionFiles -patch (1 + $afterBaseline)
} else {
    $nextPatch = (Get-PatchNumber) + 1
    Set-VersionFiles -patch $nextPatch
}
