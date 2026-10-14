# Lumen Search

A lightweight macOS Spotlight alternative for Windows.

Lumen puts your apps, files, calculations, and Windows settings within reach of a keyboard shortcut. It has a compact matte black interface, rounded corners, and real app icons.

![Lumen Search running on Windows](assets/lumen-search.png)

## Features

- **Launch apps** from a single search bar.
- **Find files** using Windows Search, with a limited local-folder fallback.
- **Open Windows settings** by typing names such as `bluetooth`, `wifi`, or `brightness`.
- **Calculate instantly** and copy the result with Enter.
- **Search the web** in your default browser.
- **Start with Windows** and stay quietly in the system tray.

## Getting started

Requires **64-bit Windows 10 or 11** and **.NET Framework 4.8**.

### Run a packaged build

Extract the package to a permanent folder and open **Lumen.exe**. Keep **Lumen.exe.config** alongside it. If another copy is running, quit it from the tray menu first.

### Build from source

Download or clone this repository, open PowerShell in the project folder, and run:

```powershell
.\build.ps1
.\Lumen.exe
```

The build script uses the .NET Framework compiler included with Windows. A separate .NET SDK is not required by this script.

## Keyboard shortcuts

| Shortcut | Action |
| --- | --- |
| **Alt + Space** | Show or hide Lumen |
| **Ctrl + Alt + Space** | Fallback if Alt + Space is occupied |
| **↑ / ↓** | Select a result |
| **Enter** | Open the selected result |
| **Ctrl + Enter** | Show a selected file in File Explorer |
| **Esc** | Hide Lumen |

You can also double-click the tray icon to open search. Clicking outside the window hides it.

## Search Windows settings

Type a name or alias, then press Enter on the Windows Settings result.

| Try searching | Opens |
| --- | --- |
| `bluetooth` | Bluetooth |
| `wifi` or `wireless` | Wi-Fi |
| `brightness` or `resolution` | Display |
| `sound` or `volume` | Sound |
| `dark mode` | Colors |
| `wallpaper` | Desktop background |
| `windows update` | Windows Update |
| `uninstall` | Installed apps |
| `startup apps` | Startup apps |
| `disk space` | Storage |
| `microphone` | Microphone permissions |
| `pc specs` | About this PC |

`settings bluetooth` and `display settings` work too. Type `settings` to see common settings results. Opening a page does not change its settings automatically. Some pages depend on your Windows version or hardware.

## Calculator

Type an expression directly into search:

```text
(20 + 5) * 4
200 * 15%
```

Press Enter on the answer to copy it to your clipboard.

## Automatic startup

On its first normal launch, Lumen registers a shortcut in your user's Startup folder. It then starts in the tray when you sign in to Windows.

Toggle **Start with Windows** from the tray menu. If you move the app, open it once from its new folder to update the shortcut.

## Things to know

- App discovery currently scans traditional Start menu shortcuts. Some Microsoft Store apps may need a shortcut added before they appear.
- File results depend on Windows indexing. If the index cannot be queried, Lumen performs a limited filename search in Desktop, Documents, and Downloads. It does not scan every drive or search document contents.
- Use **Refresh applications** after installing an app, or **Index details** to inspect a file-search error.

Lumen uses no browser engine or separate file indexer. An idle test measured approximately **35 MiB of resident memory**; usage will vary by machine.

## Contributing

Bug reports and suggestions are welcome through this repository's Issues tab. For a bug report, include your Windows version, what you searched for, what you expected, and what happened.
