# Suggest Demo (CSV Landing -> Key/Reference Suggest)

This demo shows the `meta model suggest` workflow using the real CSV landing path: import CSV files with a required `Id` column into a real workspace (`meta import csv`), then run read-only structural inference for eligible Id-based relationship promotions.

## Prerequisites

- Run from repository root.
- .NET SDK installed.

## Copy/Paste Commands (from repo root)

```powershell
if (Test-Path .\Samples\Demos\SuggestDemo\Workspace) { Remove-Item -Recurse -Force .\Samples\Demos\SuggestDemo\Workspace }

meta import csv .\Samples\Demos\SuggestDemo\demo-csv\products.csv --entity Product --new-workspace .\Samples\Demos\SuggestDemo\Workspace
Set-Location .\Samples\Demos\SuggestDemo\Workspace
meta import csv ..\demo-csv\suppliers.csv --entity Supplier
meta import csv ..\demo-csv\categories.csv --entity Category --plural Categories
meta import csv ..\demo-csv\warehouses.csv --entity Warehouse
meta import csv ..\demo-csv\orders.csv --entity Order

meta model suggest
```

CMD.exe variant (from repo root):

```cmd
if exist "Samples\Demos\SuggestDemo\Workspace" rmdir /s /q "Samples\Demos\SuggestDemo\Workspace"

meta import csv Samples\Demos\SuggestDemo\demo-csv\products.csv --entity Product --new-workspace "Samples\Demos\SuggestDemo\Workspace"
cd /d "Samples\Demos\SuggestDemo\Workspace"
meta import csv ..\demo-csv\suppliers.csv --entity Supplier
meta import csv ..\demo-csv\categories.csv --entity Category --plural Categories
meta import csv ..\demo-csv\warehouses.csv --entity Warehouse
meta import csv ..\demo-csv\orders.csv --entity Order

meta model suggest
```

## One-command runner

You can run the same sequence with:

```powershell
powershell -ExecutionPolicy Bypass -File .\Samples\Demos\SuggestDemo\run-demo.ps1
```

or

```cmd
.\Samples\Demos\SuggestDemo\run-demo.cmd
```

## Expected result notes

- Each CSV supplies explicit `Id` values, and re-import preserves those row identities.
- Use `--plural` when a landed entity needs an explicit container name such as `Category -> Categories`.
- `orders.csv` lands `ProductId`, `SupplierId`, and `WarehouseId` as scalar properties first.
- `model suggest` then prints eligible relationship promotions such as `Order.ProductId -> Product (lookup: Product.Id)`.
- Output is read-only and text-only.

