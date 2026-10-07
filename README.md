# GooseTech Chrome OS Battery Report

A local Windows app for reviewing ChromeOS battery health telemetry exported by a school’s Google Workspace for Education domain.

## Download

Download [`GooseTech_Chrome_OS_Battery_Report_Portable.zip`](downloads/GooseTech_Chrome_OS_Battery_Report_Portable.zip). It includes the Windows app, GAM7 Windows x64 archive, demo CSV files, and an ordered installation, Google Admin, GAM authorization, and export guide.

## What it does

- Imports a GAM Chrome Management telemetry CSV and a Chrome Admin Console device inventory CSV.
- Displays battery health with status colors and sorts worst-to-best, with devices missing telemetry at the bottom.
- Shows serial number, model, organizational unit, and Google’s most recent user field.
- Keeps reports local on the Windows computer. The app does not upload CSV contents.

## Requirements and setup

See [INSTALLATION.md](INSTALLATION.md) for the complete ordered setup, including GAM installation and authorization, Google Admin telemetry policy configuration, export instructions, and data handling guidance. Each school must authorize GAM using its own Workspace administrator account and Google Cloud project. Do not share GAM OAuth tokens or configuration files.

## Source

The app source is [`GooseTech_Chrome_OS_Battery_Report.cs`](GooseTech_Chrome_OS_Battery_Report.cs). The packaged executable and icon are provided in the portable ZIP.

## Privacy

The repository’s sample CSV files use fictional demo values. Do not commit real school device exports, student data, GAM configuration, OAuth tokens, client secrets, or service-account keys.

Google and ChromeOS are trademarks of Google LLC. GooseTech Chrome OS Battery Report is an independent utility and is not affiliated with or endorsed by Google.
