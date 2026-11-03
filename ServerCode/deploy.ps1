<#
.SYNOPSIS
    Deploys ServerCode/ to Unity Gaming Services with the UGS CLI: every Cloud Code script on
    deploy-list.txt. (UGS CLI 2.0.0 has no Economy support - `deploy` skips the .ecc files in
    Economy/ - so create the two currencies in the dashboard; see ServerCode/ECONOMY_CONFIG.md.)

.DESCRIPTION
    Nothing outside deploy-list.txt is sent, so the grant functions that must never go live
    (GrantCoins, GrantStardust, GrantOfflineIncome) cannot be deployed by accident. This script
    never passes --reconcile, so it never deletes anything that is already deployed.
    Setup, login and the post-deploy smoke test are in docs/deploy-runbook.md.

.PARAMETER Environment
    UGS environment name. Defaults to "development" (what AppConfig.asset targets for test builds).
    "production" asks for typed confirmation.

.PARAMETER ProjectId
    UGS project id. Defaults to cloudProjectId from ProjectSettings/ProjectSettings.asset.

.PARAMETER DryRun
    Show what would be deployed without changing anything (ugs deploy --dry-run).

.EXAMPLE
    ./ServerCode/deploy.ps1 -DryRun
.EXAMPLE
    ./ServerCode/deploy.ps1 -Environment development
#>
param(
    [string]$Environment = "development",
    [string]$ProjectId,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"
$here = $PSScriptRoot

$ugs = (Get-Command ugs -ErrorAction SilentlyContinue).Source
if (-not $ugs -and (Get-Command npm -ErrorAction SilentlyContinue)) {
    # `npm install -g ugs` puts it in npm's global folder, which isn't always on PATH.
    $candidate = Join-Path (npm prefix -g) "ugs.cmd"
    if (Test-Path $candidate) { $ugs = $candidate }
}
if (-not $ugs) {
    throw "UGS CLI ('ugs') not found on PATH or in npm's global folder. See docs/deploy-runbook.md, step 1."
}

if (-not $ProjectId) {
    $settings = Join-Path $here "..\ProjectSettings\ProjectSettings.asset"
    $match = Select-String -Path $settings -Pattern '^\s*cloudProjectId:\s*(\S+)' | Select-Object -First 1
    if (-not $match) { throw "No -ProjectId given and no cloudProjectId in $settings" }
    $ProjectId = $match.Matches[0].Groups[1].Value
}

if ($Environment -eq "production" -and -not $DryRun) {
    $answer = Read-Host "Deploy to PRODUCTION? Type 'production' to continue"
    if ($answer -ne "production") { throw "Aborted." }
}

$scripts = Get-Content (Join-Path $here "deploy-list.txt") |
    ForEach-Object { $_.Trim() } |
    Where-Object { $_ -and -not $_.StartsWith("#") }

$paths = @()
foreach ($script in $scripts) {
    $path = Join-Path $here $script
    if (-not (Test-Path $path)) { throw "deploy-list.txt names a missing file: $script" }
    $paths += $path
}
$ugsArgs = @("deploy") + $paths + @("--project-id", $ProjectId, "--environment-name", $Environment)
if ($DryRun) { $ugsArgs += "--dry-run" }

Write-Host "Deploying $($scripts.Count) Cloud Code scripts to '$Environment' (project $ProjectId)$(if ($DryRun) { ' [dry run]' })"
& $ugs @ugsArgs
if ($LASTEXITCODE -ne 0) { throw "ugs deploy failed (exit code $LASTEXITCODE)" }
