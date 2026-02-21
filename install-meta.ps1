$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$shimPath = Join-Path $repoRoot "meta.cmd"
if (-not (Test-Path $shimPath)) {
    throw "meta.cmd was not found at '$shimPath'."
}

$profilePath = $PROFILE.CurrentUserAllHosts
$profileDir = Split-Path -Parent $profilePath
if (-not (Test-Path $profileDir)) {
    New-Item -ItemType Directory -Path $profileDir | Out-Null
}

if (-not (Test-Path $profilePath)) {
    New-Item -ItemType File -Path $profilePath | Out-Null
}

$markerStart = "# META_CLI_SHIM_START"
$markerEnd = "# META_CLI_SHIM_END"
$profileText = Get-Content -Path $profilePath -Raw
$escapedShim = $shimPath.Replace("'", "''")

$snippetTemplate = @'
# META_CLI_SHIM_START
function meta {
    param([Parameter(ValueFromRemainingArguments = $true)][string[]]$Args)
    $localShim = Join-Path (Get-Location) 'meta.cmd'
    if (Test-Path $localShim) {
        & $localShim @Args
        return
    }

    $fallbackShim = '__FALLBACK_SHIM__'
    if (Test-Path $fallbackShim) {
        & $fallbackShim @Args
        return
    }

    throw "meta.cmd not found in current directory and fallback path '$fallbackShim'."
}
# META_CLI_SHIM_END
'@

$snippet = $snippetTemplate.Replace("__FALLBACK_SHIM__", $escapedShim)
$escapedStart = [regex]::Escape($markerStart)
$escapedEnd = [regex]::Escape($markerEnd)
$blockPattern = "(?ms)^\s*$escapedStart.*?$escapedEnd\s*"

if ($profileText -match $blockPattern) {
    $updated = [regex]::Replace($profileText, $blockPattern, ($snippet + [Environment]::NewLine))
    Set-Content -Path $profilePath -Value $updated -Encoding UTF8
    Write-Output "Updated meta profile shim in '$profilePath'. Restart terminal to use 'meta ...'."
    exit 0
}

if ($profileText.Length -gt 0 -and -not $profileText.EndsWith([Environment]::NewLine)) {
    $profileText += [Environment]::NewLine
}

Set-Content -Path $profilePath -Value ($profileText + $snippet + [Environment]::NewLine) -Encoding UTF8
Write-Output "Installed meta profile shim into '$profilePath'. Restart terminal to use 'meta ...'."
