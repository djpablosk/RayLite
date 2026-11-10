⚡ RayLiteA blazing-fast, fully local launcher for Windows — Spotlight / Raycast speed without the bloat.Press Ctrl + Space → type → Enter. Done.✨ Key HighlightsFeatureDescription🪶 Ultra-low RAM footprintBuilt around a ~15–20 MB idle (hidden) target: workstation GC, no WinForms/System.Drawing, native tray icon, capped index, and automated working-set trimming on hide. (See Memory notes)🚫 Zero AI bloatPurely local search. No telemetry, no cloud calls, no web APIs, no webviews, and no plugin marketplace.🔎 Instant searchFuzzy search across apps (Start Menu, packaged UWP apps, common .exe folders), files & folders (Documents, Desktop, Downloads, custom paths), and shortcuts. Includes typo tolerance.⌨️ Native Win32 hotkeyGlobal Ctrl + Space binding via RegisterHotKey (fully customizable in Settings).🎨 Minimal, dark UICentered borderless bar positioned automatically on the active monitor. Hides instantly on Esc or focus loss.🧱 Clean MVVM architectureReadable C# / WPF codebase with zero third-party framework dependencies.📦 InstallationOption 1: Installer (Recommended)Download RayLite-Setup.exe from the GitHub Releases page.Run the setup wizard. Optionally toggle Launch RayLite on startup or Create desktop shortcut.RayLite will launch directly to your system tray — press Ctrl + Space to bring up the search bar.Option 2: Portable ModeDownload RayLite.exe directly from Releases and place it anywhere on your machine. All settings persist in %APPDATA%\RayLite\.🚀 Usage & NavigationControlsShortcut / ActionFunctionCtrl + SpaceToggle launcher visibility↑ / ↓Navigate search resultsEnter / ClickLaunch selected application or open fileEsc / Click OutsideHide search barSystem TrayRight-click the RayLite tray icon for quick access to:Open LauncherSettingsRebuild IndexExit⚙️ ConfigurationOpen Settings from the tray menu to customize execution behavior:Global Hotkey: Click the keybinding box and press your desired shortcut combination.Run on Startup: Toggles automatic Windows boot execution via HKCU\Software\Microsoft\Windows\CurrentVersion\Run.Folder Indexing: Add or exclude custom scan directories (exclusions match folder names like node_modules or exact paths).Index Cap & Depth: Manage maximum indexed entries and sub-folder traversal depth.Settings are saved atomically to %APPDATA%\RayLite\settings.json:JSON{
  "hotkey": "Ctrl+Space",
  "runOnStartup": false,
  "indexDefaultFolders": true,
  "includedFolders": [],
  "excludedFolders": ["node_modules", ".git", ".vs", "__pycache__"],
  "maxIndexedFiles": 15000,
  "maxDepth": 6
}
⚠️ Hotkey Conflict Note: Ctrl + Space may conflict with certain language IMEs (e.g., Chinese/Japanese input methods) or IDE code-completion features. If a conflict occurs, rebind the hotkey in Settings (e.g., to Alt+Space or Ctrl+Alt+Space).🛠️ Building from SourcePrerequisitesWindows 10 / 11 (x64).NET 8.0 SDK(Optional) Inno Setup 6 (for creating the installer)CommandsPowerShell# 1. Clone repository
git clone https://github.com/djpablosk/RayLite.git
cd RayLite

# 2. Run in development environment
dotnet run --project src/RayLite -c Release

# 3. Publish Option A: Framework-Dependent (~200 KB)
# Requires .NET 8 Desktop Runtime installed on target machine
dotnet publish src/RayLite/RayLite.csproj -c Release -r win-x64 --self-contained false `
  -p:PublishSingleFile=true -o publish

# 4. Publish Option B: Standalone Self-Contained (~60–80 MB)
# Zero runtime requirements on target machine
dotnet publish src/RayLite/RayLite.csproj -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishReadyToRun=true `
  -o publish

# 5. Compile Installer (Outputs installer\RayLite-Setup.exe)
& "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe" setup.iss
Automated CI/CD ReleasesPushing a semantic version tag (e.g. v1.0.0) triggers the GitHub Actions Release Workflow, which automatically builds both the single-file binary and the Inno Setup installer package and attaches them to the release page.

📄 License
Distributed under the MIT License. Copyright © 2026 Patrik Smržo (djpablosk).