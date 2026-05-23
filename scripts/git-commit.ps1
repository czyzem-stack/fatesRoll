# Commit with FatesRoll hooks (version bump + README) without changing global git config.
# Usage: .\scripts\git-commit.ps1 -m "Your message"
# Extra args pass through to git commit (e.g. --amend).

param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$GitArgs
)

$root = git rev-parse --show-toplevel 2>$null
if (-not $root) { Write-Error "Not inside a git repository."; exit 1 }
Set-Location $root

& git -c core.hooksPath=.githooks commit @GitArgs
exit $LASTEXITCODE
