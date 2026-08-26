# Windows Update Diagnostic Tool

A Windows desktop application for diagnosing, monitoring, and configuring Windows Update. Built with WPF and .NET Framework 4.7.2, it provides a clean Windows 11-inspired UI for IT professionals and power users who need visibility into their Windows Update environment.

## Features

### 🔍 Diagnostics
Runs a comprehensive suite of automated checks to identify common Windows Update issues:
- **Windows Update Service** (wuauserv) — status check
- **BITS** (Background Intelligent Transfer Service) — status check
- **Cryptographic Services** — status check
- **Windows Installer Service** — status check
- **Disk Space** — flags low space that would block updates
- **Pending Reboot** — detects reboot-required registry flags
- **Update Database** — checks integrity of the Windows Update data store
- **Network Connectivity** — verifies connectivity to Windows Update endpoints
- **System Integrity** — checks for corruption that could affect update installation
- **Feature Update Blocks** — identifies policies or conditions blocking feature updates

Each check reports a **Pass / Warning / Error** status with a description and recommended remediation steps where applicable.

### 📦 Available Updates
Queries the Windows Update API for all pending software updates, displaying:
- Update title and KB article number
- Download size
- Category
- Download status (downloaded vs. not downloaded)

### ⏳ Pending Updates
Lists updates that have been downloaded but are waiting to be installed.

### 📋 Update History
Shows the history of previously installed updates including title, KB number, install date, and result status.

### ⚙️ Configuration
Reads and displays the current Windows Update configuration from the registry and WUA API:
- **Update Server Settings** — WSUS server, status server, target group
- **Auto Update Settings** — AU option, scheduled install day/time, NoAutoUpdate flag
- **Last Update Information** — last successful search and download timestamps
- **Service Status** — live status of all Windows Update-related services
- **MDM Policies** — any Mobile Device Management policies affecting Windows Update

## Screenshots

> *(Add screenshots here after first release)*

## Requirements

| Requirement | Minimum |
|---|---|
| OS | Windows 10 or Windows 11 |
| .NET Framework | 4.7.2 |
| Privileges | Administrator (recommended for full functionality) |

> ⚠️ The application will run without administrator privileges but some checks and queries may return limited or no data.

## Installation

1. Download the latest release from the [Releases](https://github.com/jgeater/WinUpdUI/releases) page
2. Extract the zip file
3. Right-click `WinUpdUI.exe` → **Run as administrator**

No installer is required. The application is portable and self-contained.

## Building from Source

### Prerequisites
- Visual Studio 2019 or later
- .NET Framework 4.7.2 SDK
- Windows 10/11 SDK (for WUApiLib COM interop)

### Steps

```bash
git clone https://github.com/jgeater/WinUpdUI.git
cd WinUpdUI
```

Open `WinUpdUI.slnx` in Visual Studio, set the configuration to **Release**, and build the solution (**Ctrl+Shift+B**).

The output will be in `WinUpdUI\bin\Release\`.

## Project Structure

```
WinUpdUI/
├── MainWindow.xaml               # Main UI layout
├── MainWindow.xaml.cs            # UI logic and event handlers
├── App.xaml                      # Application resources and styles
├── WindowsUpdateDiagnostics.cs   # Diagnostic checks engine
├── WindowsUpdateManager.cs       # Windows Update API (WUApiLib) wrapper
├── WindowsUpdateConfiguration.cs # Registry and WUA configuration reader
└── ViewportWidthConverter.cs     # WPF layout utility converter
```

## License

This project is open source. See [LICENSE](LICENSE) for details.
