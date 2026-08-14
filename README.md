# Home Assistant Automation Exporter

Windows tool to export Home Assistant `automations.yaml` entries into separate YAML files.

## Features

- Open a Windows selection window without command-line arguments.
- Select a Home Assistant `automations.yaml` file.
- Review all automations in a table with checkboxes.
- Export selected automations as individual `.yaml` files.
- Persist the export folder in `HaAutomationExporter.settings.json` next to the EXE for portable use.

## Run

```powershell
dotnet run --project .\HaAutomationExporter.csproj
```

## Command-Line Notes

The project is built as `WinExe` so the app starts without a console window. The exporter logic still accepts
command-line arguments internally, but normal use is the Windows selection window.

## Requirements

- Windows
- .NET 9 SDK
