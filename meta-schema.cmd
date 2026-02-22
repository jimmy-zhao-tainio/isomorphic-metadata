@echo off
setlocal

set "ROOT=%~dp0"
set "PROJECT=%ROOT%MetaSchema.Cli\MetaSchema.Cli.csproj"
set "OUTDIR=%ROOT%.meta-schema"
set "EXE=%OUTDIR%\meta-schema.exe"
set "NEEDS_PUBLISH=0"

if not exist "%EXE%" set "NEEDS_PUBLISH=1"

if "%NEEDS_PUBLISH%"=="0" (
  powershell -NoProfile -Command ^
    "$exe='%EXE%';" ^
    "$roots=@('%ROOT%MetaSchema.Cli','%ROOT%MetaSchema.Core','%ROOT%MetaSchema.Extractors.SqlServer','%ROOT%MetadataStudio.Core');" ^
    "$exeTime=(Get-Item $exe).LastWriteTimeUtc;" ^
    "$latest=Get-ChildItem -Path $roots -Recurse -File | Where-Object { $_.FullName -notmatch '\\\\(bin|obj)\\\\' -and ($_.Extension -eq '.cs' -or $_.Extension -eq '.csproj') } | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1;" ^
    "if ($null -eq $latest -or $latest.LastWriteTimeUtc -le $exeTime) { exit 0 } else { exit 1 }"
  if errorlevel 1 set "NEEDS_PUBLISH=1"
)

if "%NEEDS_PUBLISH%"=="1" (
  echo [meta-schema] publishing standalone binary...
  dotnet publish "%PROJECT%" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=false -o "%OUTDIR%"
  if errorlevel 1 exit /b %errorlevel%
)

"%EXE%" %*
exit /b %errorlevel%
