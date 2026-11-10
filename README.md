<div align="center">

# ⚡ RayLite

**A blazing-fast, fully local launcher for Windows — Spotlight / Raycast speed without the bloat.**

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![Platform: Windows](https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011-0078D4?logo=windows&logoColor=white)](#)

Press <kbd>Ctrl</kbd> + <kbd>Space</kbd> → type → <kbd>Enter</kbd>. Done.

</div>

---

## ✨ Key Highlights

| | |
|---|---|
| 🪶 **Ultra-low RAM footprint** | Built around a **~15–20 MB idle (hidden) target**: workstation GC, no WinForms/System.Drawing, native tray icon, a capped index, and a GC + working-set trim every time the window hides. *(Actual usage depends on your machine and runtime — see [Memory notes](#-memory-notes).)* |
| 🚫 **Zero AI bloat** | No AI, no cloud, no telemetry, no web APIs, no webviews, no plugin marketplace. Purely local search. |
| 🔎 **Instant search** | Fuzzy search across **apps** (Start Menu, Store apps, common `.exe` folders), **files & folders** (Documents, Desktop, Downloads + your own folders) and **shortcuts**. Typo-tolerant for app names. |
| ⌨️ **Native Win32 hotkey** | Global <kbd>Ctrl</kbd> + <kbd>Space</kbd> via `RegisterHotKey` — configurable in Settings. |
| 🎨 **Minimal, dark UI** | Centered borderless bar that appears on the monitor under your cursor and hides instantly on <kbd>Esc</kbd> or when you click away. |
| 🧱 **Clean MVVM codebase** | Small, readable C# / WPF project with no third-party dependencies. |

## 📦 Installation

### Option 1 — Installer (recommended)
1. Download **`RayLite-Setup.exe`** from the [Releases](../../releases) page.
2. Run it. Optionally tick **Launch RayLite on startup** and **Create a desktop shortcut**.
3. RayLite starts in the system tray — press <kbd>Ctrl</kbd> + <kbd>Space</kbd>.

### Option 2 — Portable
Download `RayLite.exe` from the Releases page and run it from anywhere. Settings live in `%APPDATA%\RayLite\`.

## 🚀 Usage

| Action | Key |
|---|---|
| Show / hide launcher | <kbd>Ctrl</kbd> + <kbd>Space</kbd> (configurable) |
| Move through results | <kbd>↑</kbd> / <kbd>↓</kbd> |
| Open selected item | <kbd>Enter</kbd> or click |
| Hide launcher | <kbd>Esc</kbd> or click outside |

**Tray icon (right-click):** Open · Settings · Rebuild index · Exit.

### Settings
Open **Settings** from the tray menu to change:
- the global **hotkey** (click the box and press a new combination),
- **Run on startup** (writes `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`),
- **included / excluded folders** (an excluded entry can be a folder name like `node_modules`, or a full path),
- the **index size limit**.

Everything is stored in `%APPDATA%\RayLite\settings.json`:

```json
{
  "hotkey": "Ctrl+Space",
  "runOnStartup": false,
  "indexDefaultFolders": true,
  "includedFolders": [],
  "excludedFolders": ["node_modules", ".git", ".vs", "__pycache__"],
  "maxIndexedFiles": 15000,
  "maxDepth": 6
}
```

> **Hotkey conflicts:** `Ctrl+Space` is also used by some IMEs (e.g. Chinese input) and by IDE auto-complete. If it clashes with something you use, pick another combination in Settings (e.g. `Alt+Space`, `Ctrl+Alt+Space`).

## 🛠️ Build from source

Requirements: Windows 10/11 x64, [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0), optionally [Inno Setup 6](https://jrsoftware.org/isinfo.php).

```powershell
git clone https://github.com/your-username/RayLite.git
cd RayLite

# Run in development
dotnet run --project src/RayLite -c Release

# A) Tiny single file (~200 KB) — requires the .NET 8 Desktop Runtime on the target PC
dotnet publish src/RayLite/RayLite.csproj -c Release -r win-x64 --self-contained false `
  -p:PublishSingleFile=true -o publish

# B) Self-contained single file (~60–80 MB) — runs anywhere, no runtime needed
dotnet publish src/RayLite/RayLite.csproj -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishReadyToRun=true `
  -o publish

# Build the installer -> installer\RayLite-Setup.exe
& "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe" setup.iss
```

> WPF does not support IL trimming (`PublishTrimmed`), which is why the self-contained build is large.

### Releasing
Push a tag such as `v1.0.0` and the [release workflow](.github/workflows/release.yml) builds the self-contained exe and the installer and attaches both to a GitHub release. Remember to keep `AppVersion` in `setup.iss` and `<Version>` in `RayLite.csproj` in sync.

## 🗂️ Repository layout

```
RayLite/
├─ RayLite.sln
├─ setup.iss                     Inno Setup 6 installer script
├─ LICENSE, README.md, .gitignore
├─ .github/workflows/release.yml Build + release on version tags
└─ src/RayLite/
   ├─ RayLite.csproj, app.manifest, app.ico, settings.json (defaults reference)
   ├─ App.xaml(.cs)              Startup, single-instance Mutex, wiring
   ├─ MainWindow.xaml(.cs)       Search bar view
   ├─ SettingsWindow.xaml(.cs)   Settings view
   ├─ Models/                    AppSettings, IndexEntry
   ├─ Native/                    NativeMethods (all P/Invokes)
   ├─ Services/                  HotKeyManager, TrayIcon, SearchEngine, AppIndexer,
   │                             FileIndexer, IndexManager, SettingsService, StartupManager,
   │                             IconLoader, ProcessLauncher, MemoryTrimmer
   └─ ViewModels/                MainViewModel, SettingsViewModel, ResultItem, RelayCommand
```

## 🧠 Memory notes

The .NET + WPF runtime has its own baseline, so RayLite is engineered to stay as small as that allows rather than promising a number on every machine:

- Non-concurrent workstation GC, Windows NLS instead of ICU, no WinForms / `System.Drawing`.
- Native `Shell_NotifyIcon` tray icon.
- Index capped (default 15,000 entries); only the 8 visible results ever load icons (small bounded cache).
- Compacting GC + working-set trim whenever the window hides or indexing finishes.

Measure the *private working set* of `RayLite.exe` (hidden, idle) in Task Manager or Process Explorer. Usage is higher while the bar is visible and while the index is rebuilding.

## 🤝 Contributing

Issues and pull requests are welcome. Please keep to the project's constraints: **local-only, no AI, no web APIs, no heavy dependencies.**

## 📄 License

[MIT](LICENSE)
