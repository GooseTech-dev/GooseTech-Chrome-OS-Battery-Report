# GooseTech Chrome OS Battery Report — installation and setup guide

This package is a local Windows viewer for ChromeOS battery telemetry. Follow these steps in order. Each school sets up its own Google Cloud project (if needed), GAM authorization, Admin Console settings, and exports. The package contains no school name, device list, telemetry export, domain setting, OAuth token, or administrator credential. The app reads the selected CSV files locally and does not upload them.

## Included files

- `GooseTech_Chrome_OS_Battery_Report.exe` — the report viewer.
- `GooseTech_Chrome_OS_Battery_Report_Source.cs` — app source code.
- `GooseTech_Chrome_OS_Battery_Report.ico` — icon file.
- `GooseTech_Export_Telemetry.bat` — exports telemetry using GAM and prompts for the GAM executable, the school’s GAM configuration folder, and the output folder.
- `GAM7/gam-7.48.22-windows-x86_64.zip` — the official GAM7 Windows x64 distribution archive. GAM’s included license is inside the archive.
- `sample_battery_telemetry.csv` and `sample_device_inventory.csv` — fictional demo data only.

## Requirements

- Windows 10 or 11.
- A Google Workspace for Education administrator authorized to set Chrome device reporting policies, export the device list, and approve the requested read-only telemetry access.
- Internet access during GAM project setup, Google sign-in, and authorization.
- Enough local disk space to extract GAM and store exports.

## Ordered setup

### 1. Extract this package

Extract the outer ZIP to a folder you control, for example `C:\ChromeOSBatteryReport`. Keep the folder in a location where the Windows account running the report can read and write files.

### 2. Extract GAM

Open the `GAM7` folder and extract `gam-7.48.22-windows-x86_64.zip` to a folder you choose, for example `C:\ChromeOSBatteryReport\GAM`. The archive contains a `gam7` folder; the executable will be at a path similar to:

`C:\ChromeOSBatteryReport\GAM\gam7\gam.exe`

Do not copy another school’s GAM configuration into this folder.

### 3. Create separate GAM configuration and working folders

Create two folders, for example:

- `C:\ChromeOSBatteryReport\GAMConfig` — GAM credentials/configuration for this school and Windows user.
- `C:\ChromeOSBatteryReport\GAMWork` — GAM working files and temporary project setup files.

GAM authorization files are sensitive. Restrict access to the GAMConfig folder to the school staff who administer this report. Do not place it in the ZIP or share it with another school.

### 4. Open Command Prompt and point GAM to those folders

Open **Command Prompt** (cmd.exe), then enter these commands, adjusting paths if you chose different folders:

```cmd
set GAMCFGDIR=C:\ChromeOSBatteryReport\GAMConfig
set PATH=%PATH%;C:\ChromeOSBatteryReport\GAM\gam7
cd /d C:\ChromeOSBatteryReport\GAMWork
gam config drive_dir C:\ChromeOSBatteryReport\GAMWork save verify
```

These settings apply to this Command Prompt window. Keep it open while doing the next two steps. If GAM’s setup screens or current official guide show a slightly different prompt, follow the GAM7 guide linked below.

### 5. Create or select the school’s Google Cloud project with GAM

In the same Command Prompt, run:

```cmd
gam create project
```

Follow GAM’s prompts and browser windows. Sign in with the school administrator account that should own/manage this setup. Create a project for this school or select a school-controlled project. Do not use a personal Google account or another school’s project. GAM’s setup can guide project creation and API setup; if the organization requires manual Cloud Console setup, use a project controlled by that organization and enable the APIs GAM prompts for, including **Chrome Management API** for telemetry.

Google Cloud project creation itself does not make the report cloud-hosted. The report and CSV output remain on the Windows computer; the project is used for Google API authorization. Google Cloud billing is not normally needed just to call these Google APIs, but each school should follow its own Google Cloud policy.

### 6. Authorize GAM with the minimum required scope

Still in the same Command Prompt, run:

```cmd
gam oauth create
```

When GAM presents the scope picker, deselect the broad default selection (the `u` command typically unselects all), then select only the scope labeled **Chrome Management API - Telemetry readonly**, and continue (`c`). Scope-picker commands can change by GAM version; use the labels and the current GAM authorization guide if the menu differs. Sign in as the school administrator and approve the request if the organization’s policies permit it.

This package uses the Admin Console CSV for inventory. Therefore, the telemetry export needs the Chrome Management telemetry read-only scope only; do not grant a broader Directory scope just for this workflow. The authorizing admin must have the Google Admin role privilege needed to read Chrome telemetry. If the organization blocks user consent, a super administrator may need to review/trust the GAM OAuth app or configure access under the school’s normal security approval process.

### 7. Enable battery telemetry in Google Admin

In the Google Admin console, go to:

**Devices → Chrome → Settings → Device settings → User and device reporting**

Select the organizational unit(s) whose devices should report. Review inherited settings, then enable **Report device telemetry** and the **Power status** telemetry option (or the corresponding battery/power option shown in the current Admin Console). Save the change. Apply it only to the intended OUs. Allow Google time to collect and display telemetry after enabling the policy; devices may need to check in.

Google’s current policy pages can rename or reorganize settings. Use the linked Google ChromeOS reporting policy documentation below if the labels differ.

### 8. Export battery telemetry to a folder you choose

Double-click `GooseTech_Export_Telemetry.bat`. Enter:

1. The full path to `gam.exe` from Step 2.
2. The GAM configuration folder from Step 3 (the folder containing this school’s `gam.cfg` and OAuth data).
3. The destination folder where you want `battery_telemetry.csv` saved. The helper creates the folder if needed.

The export command used is:

```cmd
gam print crostelemetry batteryinfo batterystatusreport
```

You can also run it in the configured Command Prompt and redirect the output to any path you choose, for example:

```cmd
gam print crostelemetry batteryinfo batterystatusreport > "D:\Battery Reports\battery_telemetry.csv"
```

### 9. Export the ChromeOS device inventory from Admin Console

In Admin Console, open **Devices → Chrome → Devices**. Select **All devices** or the desired organizational unit, then use **Export** and download the CSV when the export task is ready. If you want to choose the download location, set the browser’s download location first; otherwise, move the downloaded CSV to the folder you prefer.

The report joins telemetry with inventory fields such as device ID, serial number, model, organizational unit, and most recent user. The CSV’s **Most recent user** is Google’s reported latest device user; it does not mean the person currently signed in to a live session.

### 10. Open the report and choose the exports

Double-click `GooseTech_Chrome_OS_Battery_Report.exe`, then select **Import CSVs**. Choose the GAM telemetry CSV and then the Admin Console inventory CSV. The file dialogs allow you to select files from any folders. The app sorts battery health from worst to best, with devices missing telemetry at the bottom, and supports search/filtering and sorting controls. Demo CSVs are included if you want to inspect the display before using school data.

If the app reports missing columns, check that you exported the complete telemetry and device inventory CSVs. Do not rename the source headers.

### 11. Refresh the report later

Run `GooseTech_Export_Telemetry.bat` again and choose the same or a new destination folder. Export a fresh device inventory CSV from Admin Console, then import both current CSVs in the app. The package does not schedule exports or create a new report file on a timer; each school chooses where to save and when to refresh.

## Keep authorization and exports private

Never include `GAMConfig`, `oauth2.txt`, `client_secrets.json`, a service-account key, or other authorization files when sharing the package. These files grant access to a school’s Google data. Keep exports in a school-approved folder and share them only with authorized staff. To set up another school, that school repeats the GAM and authorization steps using its own Google account and project.

## Official references

- GAM7 [Windows installation guide](https://github.com/GAM-team/GAM/wiki/How-to-Install-GAM7)
- GAM7 [authorization guide](https://github.com/GAM-team/GAM/wiki/Authorization)
- GAM [downloads and installation files](https://github.com/GAM-team/GAM/wiki/Downloads-Installs)
- Google [Chrome Management telemetry API setup](https://support.google.com/chrome/a/answer/11230542?hl=en)
- Google [ChromeOS device details and CSV export](https://support.google.com/chrome/a/answer/1698333?hl=en)
- Google [ChromeOS user and device reporting policies](https://support.google.com/chrome/a/answer/1375678?hl=en-to)

This guide is an original, practical walkthrough of the package workflow. GAM and Google may change their screens and prompts; consult the official guides for current details.


