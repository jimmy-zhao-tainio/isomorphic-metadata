# Suggest Demo (CSV Landing -> Key/Reference Suggest)

This demo shows the `meta model suggest` workflow using the real CSV landing path: import CSV files into a real workspace (`meta import csv`) and run read-only structural inference for candidate business keys and lookup-based relationships.

## Prerequisites

- Run from repository root.
- .NET SDK installed.

## Copy/Paste Commands (from repo root)

```powershell
$workspace = ".\Samples\SuggestDemo\Workspace"
if (Test-Path $workspace) { Remove-Item -Recurse -Force $workspace }

dotnet run --project Meta.Cli/Meta.Cli.csproj -- import csv .\Samples\SuggestDemo\demo-csv\products.csv --entity Product --new-workspace $workspace
dotnet run --project Meta.Cli/Meta.Cli.csproj -- import csv .\Samples\SuggestDemo\demo-csv\suppliers.csv --entity Supplier --workspace $workspace
dotnet run --project Meta.Cli/Meta.Cli.csproj -- import csv .\Samples\SuggestDemo\demo-csv\categories.csv --entity Category --workspace $workspace
dotnet run --project Meta.Cli/Meta.Cli.csproj -- import csv .\Samples\SuggestDemo\demo-csv\warehouses.csv --entity Warehouse --workspace $workspace
dotnet run --project Meta.Cli/Meta.Cli.csproj -- import csv .\Samples\SuggestDemo\demo-csv\orders.csv --entity Order --workspace $workspace

dotnet run --project Meta.Cli/Meta.Cli.csproj -- model suggest --workspace $workspace
```

## One-command runner

You can run the same sequence with:

```powershell
powershell -ExecutionPolicy Bypass -File .\Samples\SuggestDemo\run-demo.ps1
```

or

```cmd
.\Samples\SuggestDemo\run-demo.cmd
```

## Expected result notes

- `model suggest` lists candidate business keys such as `Warehouse.WarehouseCode`, `Product.ProductCode`, and `Supplier.SupplierCode`.
- `model suggest` lists candidate relationship refactors such as `Order.WarehouseCode -> Warehouse.WarehouseCode`.
- Output is read-only and text-only.
