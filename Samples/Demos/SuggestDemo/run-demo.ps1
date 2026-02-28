param(
    [string]$WorkspacePath = "Samples/Demos/SuggestDemo/Workspace"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest
$PSNativeCommandUseErrorActionPreference = $false

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..\..")).Path
Set-Location $repoRoot

$workspace = Join-Path $repoRoot ($WorkspacePath.Replace('/', '\'))
$csvRoot = Join-Path $repoRoot "Samples\Demos\SuggestDemo\demo-csv"
$metaExe = Join-Path $repoRoot "meta.exe"

if (-not (Test-Path $metaExe))
{
    throw "meta.exe not found at '$metaExe'."
}

if (Test-Path $workspace)
{
    Remove-Item -Recurse -Force $workspace
}

function Invoke-MetaStrict {
    param([string[]]$MetaArgs)

    $previousErrorActionPreference = $ErrorActionPreference
    try
    {
        $ErrorActionPreference = "Continue"
        & $metaExe @MetaArgs
    }
    finally
    {
        $ErrorActionPreference = $previousErrorActionPreference
    }

    if ($LASTEXITCODE -ne 0)
    {
        throw "Command failed (exit $LASTEXITCODE): meta $($MetaArgs -join ' ')"
    }
}

Write-Host "== Import CSV landing entities =="
Invoke-MetaStrict -MetaArgs @("import", "csv", (Join-Path $csvRoot "products.csv"), "--entity", "Product", "--new-workspace", $workspace)
Push-Location $workspace
try
{
    Invoke-MetaStrict -MetaArgs @("import", "csv", "..\demo-csv\suppliers.csv", "--entity", "Supplier")
    Invoke-MetaStrict -MetaArgs @("import", "csv", "..\demo-csv\categories.csv", "--entity", "Category", "--plural", "Categories")
    Invoke-MetaStrict -MetaArgs @("import", "csv", "..\demo-csv\warehouses.csv", "--entity", "Warehouse")
    Invoke-MetaStrict -MetaArgs @("import", "csv", "..\demo-csv\orders.csv", "--entity", "Order")

    Write-Host ""
    Write-Host "== Suggest pass (eligible Id-based relationship promotions) =="
    Invoke-MetaStrict -MetaArgs @("model", "suggest")
}
finally
{
    Pop-Location
}

Write-Host ""
Write-Host "OK: suggest demo complete"
Write-Host "Workspace: $workspace"

