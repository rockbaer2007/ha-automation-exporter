# Home Assistant Automation Exporter

Windows tool to export Home Assistant `automations.yaml` entries into separate YAML files.

## Features

- Open a Windows selection window without command-line arguments.
- Select a Home Assistant `automations.yaml` file.
- Review all automations in a table with checkboxes.
- Export selected automations as individual `.yaml` files.
- Persist the export folder in the per-user `.NET` `user.config`.
- Continue to support full export from the command line.

## Run

```powershell
dotnet run --project .\HaAutomationExporter.csproj
```

## Full Command-Line Export

```powershell
dotnet run --project .\HaAutomationExporter.csproj -- C:\Users\rockb\Downloads\automations.yaml C:\Users\rockb\Downloads\automations-export
```

## Requirements

- Windows
- .NET 10 SDK

