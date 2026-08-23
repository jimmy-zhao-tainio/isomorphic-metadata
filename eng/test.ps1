[CmdletBinding()]
param(
    [ValidateSet("Unit", "Integration", "Performance100K", "Performance1M")]
    [string]$Profile = "Unit",

    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",

    [ValidateRange(1, 12)]
    [int]$MaxConcurrency = 4
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))

function ConvertTo-ProcessArgument
{
    param(
        [AllowEmptyString()]
        [Parameter(Mandatory = $true)]
        [string]$Value
    )

    if ($Value.Length -gt 0 -and $Value -notmatch '[\s"]')
    {
        return $Value
    }

    $escaped = [regex]::Replace($Value, '(\\*)"', '$1$1\"')
    $escaped = [regex]::Replace($escaped, '(\\+)$', '$1$1')
    return '"' + $escaped + '"'
}

function New-TestCase
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,

        [Parameter(Mandatory = $true)]
        [string]$Project,

        [string]$Filter,

        [hashtable]$Environment = @{},

        [ValidateRange(1, 60)]
        [int]$TimeoutMinutes = 8
    )

    return [pscustomobject]@{
        Name = $Name
        Project = $Project
        Filter = $Filter
        Environment = $Environment
        TimeoutMinutes = $TimeoutMinutes
    }
}

function Start-TestCase
{
    param(
        [Parameter(Mandatory = $true)]
        [psobject]$TestCase
    )

    $projectPath = [IO.Path]::GetFullPath((Join-Path $repoRoot $TestCase.Project))
    if (-not (Test-Path -LiteralPath $projectPath -PathType Leaf))
    {
        throw "Test project '$projectPath' does not exist."
    }

    $resultDirectoryName = $TestCase.Name -replace '[^A-Za-z0-9.-]', '-'
    $resultDirectory = Join-Path $resultsRoot $resultDirectoryName
    New-Item -ItemType Directory -Path $resultDirectory | Out-Null
    $resultPath = Join-Path $resultDirectory "results.trx"
    $arguments = @(
        "test",
        $projectPath,
        "--configuration",
        $Configuration,
        "--no-build",
        "--no-restore",
        "--nologo",
        "--logger",
        "trx;LogFileName=results.trx",
        "--results-directory",
        $resultDirectory,
        "-m:1",
        "-nr:false"
    )
    if (-not [string]::IsNullOrWhiteSpace($TestCase.Filter))
    {
        $arguments += @("--filter", $TestCase.Filter)
    }

    $startInfo = New-Object Diagnostics.ProcessStartInfo
    $startInfo.FileName = "dotnet"
    $startInfo.Arguments = ($arguments | ForEach-Object { ConvertTo-ProcessArgument $_ }) -join " "
    $startInfo.WorkingDirectory = $repoRoot
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    foreach ($entry in $TestCase.Environment.GetEnumerator())
    {
        $startInfo.EnvironmentVariables[$entry.Key] = [string]$entry.Value
    }

    $process = New-Object Diagnostics.Process
    $process.StartInfo = $startInfo
    if (-not $process.Start())
    {
        $process.Dispose()
        throw "Could not start test project '$($TestCase.Name)'."
    }

    Write-Host "[START] $($TestCase.Name)"
    return [pscustomobject]@{
        TestCase = $TestCase
        Process = $process
        StandardOutput = $process.StandardOutput.ReadToEndAsync()
        StandardError = $process.StandardError.ReadToEndAsync()
        ResultPath = $resultPath
        Stopwatch = [Diagnostics.Stopwatch]::StartNew()
    }
}

function Stop-TestProcessTree
{
    param(
        [Parameter(Mandatory = $true)]
        [Diagnostics.Process]$Process
    )

    try
    {
        if (-not $Process.HasExited)
        {
            & taskkill.exe /PID $Process.Id /T /F 2>&1 | Out-Null
        }
    }
    catch
    {
        try
        {
            if (-not $Process.HasExited)
            {
                $Process.Kill()
            }
        }
        catch
        {
        }
    }
}

function Format-Elapsed
{
    param(
        [Parameter(Mandatory = $true)]
        [timespan]$Elapsed
    )

    return "{0:00}:{1:00}.{2:0}" -f [math]::Floor($Elapsed.TotalMinutes), $Elapsed.Seconds, [math]::Floor($Elapsed.Milliseconds / 100)
}

function Get-TestExecutionResult
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$ResultPath
    )

    if (-not (Test-Path -LiteralPath $ResultPath -PathType Leaf))
    {
        throw "Test result file was not produced: $ResultPath"
    }

    [xml]$resultDocument = Get-Content -LiteralPath $ResultPath -Raw
    $testResults = @($resultDocument.TestRun.Results.UnitTestResult)
    if ($testResults.Count -eq 0)
    {
        throw "Test result file contains no unit-test results: $ResultPath"
    }

    $milliseconds = 0.0
    foreach ($testResult in $testResults)
    {
        $milliseconds += [timespan]::Parse(
            [string]$testResult.duration,
            [Globalization.CultureInfo]::InvariantCulture).TotalMilliseconds
    }

    return [pscustomobject]@{
        Count = $testResults.Count
        ExecutionMilliseconds = [long][math]::Ceiling($milliseconds)
    }
}

$unitTests = @(
    (New-TestCase "Operations unit" "Meta\Operations.Tests\Meta.Operations.Tests.csproj"),
    (New-TestCase "Core unit" "Meta\Tests\Meta.Core.Tests.csproj")
)

$integrationTests = @(
    (New-TestCase "MetaDocs" "MetaDocs\Tests\MetaDocs.Tests.csproj"),
    (New-TestCase "Meta integration" "Meta\Integration.Tests\Meta.Integration.Tests.csproj"),
    (New-TestCase "MetaMesh" "MetaMesh\Tests\MetaMesh.Tests.csproj"),
    (New-TestCase "MetaWeave" "MetaWeave\Tests\MetaWeave.Tests.csproj"),
    (New-TestCase "MetaCli" "MetaCli\Tests\MetaCli.Tests.csproj"),
    (New-TestCase "C# surface" "Meta\Surfaces.CSharp.Tests\Meta.Surfaces.CSharp.Tests.csproj"),
    (New-TestCase "MetaWeaveScript" "MetaWeave\Script\Tests\MetaWeaveScript.Tests.csproj"),
    (New-TestCase "Architecture" "Meta\Architecture.Tests\Meta.Architecture.Tests.csproj"),
    (New-TestCase "SQL surface" "Meta\Surfaces.Sql.Tests\Meta.Surfaces.Sql.Tests.csproj"),
    (New-TestCase "XML surface" "Meta\Surfaces.Xml.Tests\Meta.Surfaces.Xml.Tests.csproj" "FullyQualifiedName!~LargeWorkspacePerformanceTests")
)

switch ($Profile)
{
    "Unit"
    {
        $testCases = $unitTests
    }
    "Integration"
    {
        $testCases = $integrationTests
    }
    "Performance100K"
    {
        $testCases = @(
            (New-TestCase `
                "XML surface 100k performance" `
                "Meta\Surfaces.Xml.Tests\Meta.Surfaces.Xml.Tests.csproj" `
                "FullyQualifiedName~LargeWorkspacePerformanceTests.SaveAndLoad_100kRows_WithinConfiguredBudgets" `
                @{} `
                3)
        )
    }
    "Performance1M"
    {
        $testCases = @(
            (New-TestCase `
                "XML surface 1m performance" `
                "Meta\Surfaces.Xml.Tests\Meta.Surfaces.Xml.Tests.csproj" `
                "FullyQualifiedName~LargeWorkspacePerformanceTests.SaveAndLoad_1MRows_WithinConfiguredBudgets_WhenEnabled" `
                @{ Meta_ENABLE_1M_PERF_TEST = "1" } `
                15)
        )
    }
}

$tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$resultsRoot = [IO.Path]::GetFullPath((Join-Path $tempRoot ("meta-test-results-" + [guid]::NewGuid().ToString("N"))))
if (-not $resultsRoot.StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase))
{
    throw "Resolved test-results directory escaped the system temporary directory: $resultsRoot"
}
New-Item -ItemType Directory -Path $resultsRoot | Out-Null
$overallStopwatch = [Diagnostics.Stopwatch]::StartNew()

$pending = New-Object 'Collections.Generic.Queue[object]'
foreach ($testCase in $testCases)
{
    $pending.Enqueue($testCase)
}

$active = New-Object Collections.ArrayList
$failures = New-Object Collections.ArrayList
$totalTests = 0
$totalTestExecutionMilliseconds = 0
$unitAssemblyExecutionBudgetMilliseconds = 1000
try
{
    while ($pending.Count -gt 0 -or $active.Count -gt 0)
    {
        while ($failures.Count -eq 0 -and $pending.Count -gt 0 -and $active.Count -lt $MaxConcurrency)
        {
            [void]$active.Add((Start-TestCase $pending.Dequeue()))
        }

        $timedOut = @($active | Where-Object {
            -not $_.Process.HasExited -and
            $_.Stopwatch.Elapsed.TotalMinutes -ge $_.TestCase.TimeoutMinutes
        })
        foreach ($run in $timedOut)
        {
            Stop-TestProcessTree $run.Process
            if (-not $run.Process.WaitForExit(5000))
            {
                $run.Process.Kill()
                $run.Process.WaitForExit()
            }

            $run.Stopwatch.Stop()
            $standardOutput = $run.StandardOutput.GetAwaiter().GetResult()
            $standardError = $run.StandardError.GetAwaiter().GetResult()
            $run.Process.Dispose()
            [void]$active.Remove($run)
            Write-Host "[TIMEOUT] $($run.TestCase.Name) after $(Format-Elapsed $run.Stopwatch.Elapsed)" -ForegroundColor Red
            [void]$failures.Add([pscustomobject]@{
                Name = $run.TestCase.Name
                ExitCode = 124
                StandardOutput = $standardOutput
                StandardError = $standardError
            })
        }

        if ($failures.Count -gt 0 -and $active.Count -eq 0)
        {
            break
        }

        $completed = @($active | Where-Object { $_.Process.HasExited })
        if ($completed.Count -eq 0)
        {
            Start-Sleep -Milliseconds 100
            continue
        }

        foreach ($run in $completed)
        {
            $run.Process.WaitForExit()
            $run.Stopwatch.Stop()
            $standardOutput = $run.StandardOutput.GetAwaiter().GetResult()
            $standardError = $run.StandardError.GetAwaiter().GetResult()
            $exitCode = $run.Process.ExitCode
            $run.Process.Dispose()
            [void]$active.Remove($run)

            if ($exitCode -eq 0)
            {
                $testResult = Get-TestExecutionResult $run.ResultPath
                $testCount = $testResult.Count
                $testExecutionMilliseconds = $testResult.ExecutionMilliseconds

                $totalTests += $testCount
                $totalTestExecutionMilliseconds += $testExecutionMilliseconds
                Write-Host "[PASS] $($run.TestCase.Name) - $testCount test(s), $testExecutionMilliseconds ms summed test execution, $(Format-Elapsed $run.Stopwatch.Elapsed) process"
                if ($Profile -eq "Unit" -and $testExecutionMilliseconds -ge $unitAssemblyExecutionBudgetMilliseconds)
                {
                    [void]$failures.Add([pscustomobject]@{
                        Name = "$($run.TestCase.Name) test execution budget"
                        ExitCode = 1
                        StandardOutput = "$testExecutionMilliseconds ms must remain below the $unitAssemblyExecutionBudgetMilliseconds ms unit assembly budget."
                        StandardError = ""
                    })
                }
            }
            else
            {
                Write-Host "[FAIL] $($run.TestCase.Name) - exit $exitCode after $(Format-Elapsed $run.Stopwatch.Elapsed)" -ForegroundColor Red
                [void]$failures.Add([pscustomobject]@{
                    Name = $run.TestCase.Name
                    ExitCode = $exitCode
                    StandardOutput = $standardOutput
                    StandardError = $standardError
                })
            }
        }

        if ($failures.Count -gt 0 -and $active.Count -eq 0)
        {
            break
        }
    }
}
finally
{
    foreach ($run in @($active))
    {
        Stop-TestProcessTree $run.Process
        $run.Process.Dispose()
    }

    if (Test-Path -LiteralPath $resultsRoot -PathType Container)
    {
        Remove-Item -LiteralPath $resultsRoot -Recurse -Force
    }
}

$overallStopwatch.Stop()
if ($failures.Count -gt 0)
{
    foreach ($failure in $failures)
    {
        Write-Host ""
        Write-Host "===== $($failure.Name) stdout =====" -ForegroundColor Red
        Write-Host $failure.StandardOutput
        if (-not [string]::IsNullOrWhiteSpace($failure.StandardError))
        {
            Write-Host "===== $($failure.Name) stderr =====" -ForegroundColor Red
            Write-Host $failure.StandardError
        }
    }

    Write-Host "[FAILED] $Profile in $(Format-Elapsed $overallStopwatch.Elapsed)." -ForegroundColor Red
    exit 1
}

Write-Host "[PASSED] $Profile - $totalTests test(s), $totalTestExecutionMilliseconds ms summed test execution, $(Format-Elapsed $overallStopwatch.Elapsed) process."
