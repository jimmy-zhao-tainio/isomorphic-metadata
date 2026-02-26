param(
    [string]$WorkspacePath = "Samples/SuggestDemo/Workspace"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest
$PSNativeCommandUseErrorActionPreference = $false

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
Set-Location $repoRoot

$workspace = Join-Path $repoRoot ($WorkspacePath.Replace('/', '\'))
$csvRoot = Join-Path $repoRoot "Samples\SuggestDemo\demo-csv"
$metaProject = Join-Path $repoRoot "Meta.Cli\Meta.Cli.csproj"

if (Test-Path $workspace)
{
    Remove-Item -Recurse -Force $workspace
}

function Invoke-MetaStrict {
    param([string[]]$MetaArgs)

    $args = @("run", "--project", $metaProject, "--") + $MetaArgs
    $previousErrorActionPreference = $ErrorActionPreference
    try
    {
        $ErrorActionPreference = "Continue"
        & dotnet @args
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
Invoke-MetaStrict -MetaArgs @("import", "csv", (Join-Path $csvRoot "suppliers.csv"), "--entity", "Supplier", "--workspace", $workspace)
Invoke-MetaStrict -MetaArgs @("import", "csv", (Join-Path $csvRoot "categories.csv"), "--entity", "Category", "--workspace", $workspace)
Invoke-MetaStrict -MetaArgs @("import", "csv", (Join-Path $csvRoot "warehouses.csv"), "--entity", "Warehouse", "--workspace", $workspace)
Invoke-MetaStrict -MetaArgs @("import", "csv", (Join-Path $csvRoot "orders.csv"), "--entity", "Order", "--workspace", $workspace)

Write-Host ""
Write-Host "== Suggest pass (business keys + lookup relationships) =="
Invoke-MetaStrict -MetaArgs @("model", "suggest", "--workspace", $workspace)

Write-Host ""
Write-Host "OK: suggest demo complete"
Write-Host "Workspace: $workspace"
