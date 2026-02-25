param(
    [string]$OutputRoot = "Sanctioned.Generated"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest
$PSNativeCommandUseErrorActionPreference = $false

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
Set-Location $repoRoot

$metaProject = Join-Path $repoRoot "Meta.Cli\Meta.Cli.csproj"
if (-not (Test-Path $metaProject))
{
    throw "Meta CLI project was not found at '$metaProject'."
}

$resolvedOutputRoot = Join-Path $repoRoot $OutputRoot
if (Test-Path $resolvedOutputRoot)
{
    Remove-Item $resolvedOutputRoot -Recurse -Force
}

New-Item -ItemType Directory -Path $resolvedOutputRoot -Force | Out-Null

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

$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("meta-sanctioned-" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $tempRoot -Force | Out-Null

try
{
    $metaWorkspaceModel = Join-Path $repoRoot "Meta.Core\WorkspaceConfig\Models\MetaWorkspace.model.xml"
    $metaWorkspaceInstance = Join-Path $repoRoot "Meta.Core\WorkspaceConfig\Models\MetaWorkspace.instance.empty.xml"
    $metaWorkspaceTemp = Join-Path $tempRoot "MetaWorkspaceWorkspace"
    Invoke-MetaStrict -MetaArgs @("import", "xml", $metaWorkspaceModel, $metaWorkspaceInstance, "--new-workspace", $metaWorkspaceTemp)
    Invoke-MetaStrict -MetaArgs @("generate", "csharp", "--tooling", "--out", (Join-Path $resolvedOutputRoot "MetaWorkspace"), "--workspace", $metaWorkspaceTemp)

    $schemaCatalogModel = Join-Path $repoRoot "MetaSchema.Core\Models\SchemaCatalog.model.xml"
    $schemaCatalogInstance = Join-Path $repoRoot "MetaSchema.Core\Models\SchemaCatalog.instance.empty.xml"
    $schemaCatalogTemp = Join-Path $tempRoot "SchemaCatalogWorkspace"
    Invoke-MetaStrict -MetaArgs @("import", "xml", $schemaCatalogModel, $schemaCatalogInstance, "--new-workspace", $schemaCatalogTemp)
    Invoke-MetaStrict -MetaArgs @("generate", "csharp", "--tooling", "--out", (Join-Path $resolvedOutputRoot "SchemaCatalog"), "--workspace", $schemaCatalogTemp)

    $typeConversionWorkspace = Join-Path $repoRoot "MetaSchema.Catalogs\TypeConversionCatalog"
    Invoke-MetaStrict -MetaArgs @("generate", "csharp", "--tooling", "--out", (Join-Path $resolvedOutputRoot "TypeConversionCatalog"), "--workspace", $typeConversionWorkspace)
}
finally
{
    if (Test-Path $tempRoot)
    {
        Remove-Item $tempRoot -Recurse -Force
    }
}

Write-Host "OK: sanctioned model APIs generated with tooling"
Write-Host "Out: $resolvedOutputRoot"
