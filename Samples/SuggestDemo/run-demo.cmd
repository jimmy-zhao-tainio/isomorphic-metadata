@echo off
setlocal
set "ROOT=%~dp0..\.."
set "WORKSPACE=Samples\SuggestDemo\Workspace"

pushd "%ROOT%" || exit /b 1

if exist "%WORKSPACE%" rmdir /s /q "%WORKSPACE%"

echo == Import CSV landing entities ==
"%ROOT%\meta.exe" import csv Samples\SuggestDemo\demo-csv\products.csv --entity Product --new-workspace "%WORKSPACE%" || goto :fail

pushd "%WORKSPACE%" || goto :fail
"%ROOT%\meta.exe" import csv ..\demo-csv\suppliers.csv --entity Supplier || goto :fail
"%ROOT%\meta.exe" import csv ..\demo-csv\categories.csv --entity Category || goto :fail
"%ROOT%\meta.exe" import csv ..\demo-csv\warehouses.csv --entity Warehouse || goto :fail
"%ROOT%\meta.exe" import csv ..\demo-csv\orders.csv --entity Order || goto :fail

echo.
echo == Suggest pass (business keys + lookup relationships) ==
"%ROOT%\meta.exe" model suggest || goto :fail

echo.
echo OK: suggest demo complete
echo Workspace: %CD%
popd
popd
exit /b 0

:fail
set "EC=%ERRORLEVEL%"
echo.
echo Demo failed with exit code %EC%.
if exist "%CD%\metadata\workspace.xml" popd
popd
exit /b %EC%
