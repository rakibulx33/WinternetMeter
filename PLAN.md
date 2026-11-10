# WinternetMeter — Windows 11 Comfort + Linux (Ubuntu) Port Plan

> Build plan for Claude Code. Work phase by phase; each phase ends in a working, releasable app.
> Commit after every numbered task. Don't start Phase 3 until Phase 2's tests pass.

---

## 0. Where the repo stands (audit, commit `ba603b4`)

**Stack:** C# WinForms, `net6.0-windows`, tray app (`TrayAppContext`) + borderless floating window (`FloatingSpeedForm`), speed from `NetworkInterface.GetIPv4Statistics()` sampled every 800 ms, settings via `Properties.Settings`.

### Blockers for "comfortable on Windows 11"

| # | Problem | Where | Why it matters |
|---|---------|-------|----------------|
| B1 | Targets **.NET 6** (out of support since Nov 2024). .NET 8/9 also went out of support on 10 Nov 2026. | `WinternetMeter.csproj` | User must install an old runtime; no security fixes. |
| B2 | **No release binary.** README says "open in Visual Studio and build". | README | Non-developers can't use it. |
| B3 | **Auto-select picks the wrong adapter on Win 11.** It sorts by `nic.Speed`; WSL/Hyper-V `vEthernet` adapters report ~10 Gbps, so they win → meter shows 0. Also, auto-select only runs when you click the menu item; on startup it uses the first "Up" adapter. | `TrayAppContext.GetBestAdapterName`, constructor | The core feature silently shows 0 KB/s. |
| B4 | **Adapter choice is never saved.** `ChangeAdapter()` (the only method that saves) is never called. | `TrayAppContext` | Manual pick is lost on restart. |
| B5 | **No recovery after sleep / Wi-Fi switch / VPN.** The `NetworkInterface` object is captured once; adapter menu is built once. | `NetworkMonitor`, `BuildContextMenu` | Meter freezes at 0 after resume until restart. |
| B6 | **Saved position can be off-screen** (unplugged monitor, resolution change). Position only saved on clean Exit, not on drag or Windows shutdown. | `TrayAppContext`, `ExitThreadCore` | Window "disappears". |
| B7 | **Blurry on mixed-DPI setups** — no PerMonitorV2 DPI mode. | csproj | Most Win 11 laptops + external monitor. |
| B8 | **Window jitters** — resized every tick from `MeasureString`, grows rightward from its left edge. | `FloatingSpeedForm.SetSpeed` | Distracting next to the clock. |
| B9 | **Ugly transparency** — `TransparencyKey = Black` gives black fringes on anti-aliased text, makes black text impossible, and the color-emoji arrows render poorly in GDI. `Opacity` and `TextBackColor` settings exist but are unused. | `FloatingSpeedForm` | Looks unpolished on Win 11. |
| B10 | Floating window **shows in Alt+Tab** and **steals focus** when clicked. | `FloatingSpeedForm` | Annoying while typing/gaming. |
| B11 | **Hidden behind the taskbar** after clicking the taskbar; **covers fullscreen games/videos**. | `FloatingSpeedForm` | Common Win 11 complaint for overlay widgets. |
| B12 | **No single-instance guard** — startup entry + manual launch = two meters. | `Program.cs` | |
| B13 | Startup registry value is the **unquoted** path (contains a space: `Winternet Meter.exe`); goes stale if exe moves. | `SetStartup` | |
| B14 | `Properties.Settings` stores under a **hashed per-path/per-version folder** → settings vanish on update or move. Also Windows-only. | `Properties/` | Breaks upgrades; blocks Linux. |
| B15 | Color menu sets `Checked` inside `Paint` → repaint loop. Font size uses Bold, startup uses Regular. | `TrayAppContext`, `FloatingSpeedForm` | Small bugs. |
| B16 | Only KB/s / MB/s (bytes). ISPs advertise **Mbps**. | `UpdateSpeed` | |

### Repo hygiene
- `obj/` and `WinternetMeter.csproj.user` committed; no `.gitignore`.
- Dead code: `Form1.*`, empty `SettingsForm.cs`, unused `ChangeAdapter`, `FixWindowPosition`.
- `demo2.gif` is **11.8 MB** — every clone pays for it.
- README claims Windows 7/8 support — false for any supported .NET. README says MIT but there's no `LICENSE` file.

---

## Locked decisions

| Decision | Choice | Reason |
|---|---|---|
| Runtime | **.NET 10 (LTS, supported to Nov 2028)** | Only supported LTS now. |
| Distribution | **Self-contained single-file `.exe`** + optional Inno Setup installer | Zero prerequisites for the user. |
| Settings | **JSON file**: `%AppData%\WinternetMeter\settings.json` / `~/.config/winternetmeter/settings.json` | Survives updates, human-readable, cross-platform. |
| Architecture | Split into **`WinternetMeter.Core`** (no UI, `net10.0`) + UI front-ends | Core is the part that ports to Linux unchanged. |
| Windows UI | **Keep WinForms** for now | It works; rewriting is not the bottleneck. |
| Linux UI | **Avalonia** front-end on the same Core | Same language, same Core; WinForms doesn't run on Linux. |
| Speed source | `NetworkInterface.GetIPStatistics()` (not `GetIPv4Statistics`) | Counts IPv6 too; supported on Windows and Linux (`/proc/net/dev` under the hood). |
| Default adapter mode | **"Auto" = adapter(s) carrying the default route**, excluding virtual adapters | Fixes B3 without the user knowing what an adapter is. |

---

## Target layout

```
WinternetMeter.sln
src/
  WinternetMeter.Core/            # net10.0, no UI
    SpeedSampler.cs               # counters → bytes/sec, handles resets & wraparound
    AdapterResolver.cs            # Auto mode + filtering + change events
    SpeedFormatter.cs             # bytes/bits, KB/MB, fixed-width
    AppSettings.cs                # POCO + JsonSettingsStore
    IAutostart.cs                 # interface; platform impls live in UI projects
  WinternetMeter.Windows/         # net10.0-windows, WinForms (current app, refactored)
  WinternetMeter.Linux/           # net10.0, Avalonia (Phase 3)
tests/
  WinternetMeter.Core.Tests/      # xUnit
installer/
  winternetmeter.iss              # Inno Setup
.github/workflows/
  ci.yml
  release.yml
```

---

## Phase 1 — Windows 11 quick wins (small diffs, no restructuring)

Goal: someone downloads one `.exe`, runs it, and it shows the right speed. Ship as **v1.1.0**.

1. **Repo hygiene**
   - Add `.gitignore` (VisualStudio template). `git rm -r --cached obj WinternetMeter.csproj.user`.
   - Delete `Form1.cs`, `Form1.Designer.cs`, `SettingsForm.cs`, `ChangeAdapter`, `FixWindowPosition`.
   - Add `LICENSE` (MIT, "Rakibul Hasan").
   - ✅ `dotnet build` passes, no behavior change.

2. **Upgrade to .NET 10**
   - csproj: `<TargetFramework>net10.0-windows</TargetFramework>`, add `<ApplicationHighDpiMode>PerMonitorV2</ApplicationHighDpiMode>` (fixes B7), `<Version>1.1.0</Version>`.
   - Remove the `Microsoft.VisualBasic` InputBox usage (replace with `ColorDialog`, see task 7).
   - ✅ Builds with .NET 10 SDK; text is sharp on a 150% monitor.

3. **Fix adapter selection (B3, B4, B5)**
   - Replace `GetIPv4Statistics()` with `GetIPStatistics()`.
   - New Auto logic: adapters that are `Up`, not `Loopback`/`Tunnel`, **have a default gateway** (`GetIPProperties().GatewayAddresses` non-empty), and whose `Description`/`Name` doesn't match `vEthernet|Hyper-V|WSL|VirtualBox|VMware|TAP|Wintun|Npcap|Loopback`. If several match, **sum them**.
   - Run Auto on startup when `AutoSelectAdapter` is true (currently it doesn't).
   - Persist manual choice by **adapter `Id`** (GUID), not `Name` (name can change).
   - Subscribe to `NetworkChange.NetworkAddressChanged` and `NetworkAvailabilityChanged` and `SystemEvents.PowerModeChanged` (Resume): re-resolve adapter, reset sampler, rebuild the Adapter submenu (marshal to UI thread with `BeginInvoke`).
   - In the sampler: if a counter goes **down** (adapter reset), treat it as a fresh baseline instead of reporting a negative speed.
   - ✅ With WSL installed, Auto picks Wi-Fi/Ethernet. Sleep → resume → speed recovers within 2 s. Toggle Wi-Fi off/on → recovers.

4. **Floating window behaves like a widget (B10, B11)**
   - Override `CreateParams`: add `WS_EX_TOOLWINDOW` (hide from Alt+Tab) and `WS_EX_NOACTIVATE` (don't steal focus). Override `ShowWithoutActivation => true`.
   - Re-assert topmost every ~2 s with `SetWindowPos(HWND_TOPMOST, …, SWP_NOMOVE|SWP_NOSIZE|SWP_NOACTIVATE)` so the taskbar can't bury it.
   - Fullscreen detection: `SHQueryUserNotificationState` → if `QUNS_BUSY` / `QUNS_RUNNING_D3D_FULL_SCREEN` / `QUNS_PRESENTATION_MODE`, hide the window; show again when it clears. Menu toggle **"Hide in fullscreen"** (default on).
   - ✅ Not in Alt+Tab; clicking it doesn't defocus the editor; hidden during a fullscreen YouTube/game.

5. **Position that never gets lost (B6)**
   - Save position on drag end (`MouseUp` / `WM_EXITSIZEMOVE`), not only on Exit. Handle `SystemEvents.SessionEnding` to save.
   - On startup and on `SystemEvents.DisplaySettingsChanged`: if the window rect doesn't intersect any `Screen.AllScreens[i].WorkingArea`, snap back to bottom-right of primary.
   - Tray menu item **"Reset position"**.
   - ✅ Unplug the external monitor → widget comes back on the laptop screen.

6. **Stop the jitter + clean look (B8, B9, B15)**
   - Fixed-width output: pad numbers (`{value,6:F1}`), use a monospace-friendly default (`Cascadia Mono` → fallback `Consolas`), size the form **once** per font/size change using the widest possible string (`"999.9 MB/s"`), not every tick.
   - Anchor the form to the **corner nearest its screen edge** so growth doesn't push it off the clock.
   - Drop `TransparencyKey`. Draw a **rounded dark pill** (radius 8) in `OnPaint` with `Opacity` from settings (default 0.85), and add a **"Background: Pill / None"** option; "None" keeps the old look but uses a near-black key (`#010101`) to kill the fringe.
   - Replace color emoji `⬆️⬇️` with plain glyphs `↑ ↓` (render cleanly in GDI).
   - One font style everywhere (Bold). Remove the `Paint` handler on color items; update checks in the click handler only.
   - ✅ Numbers change, the box doesn't move or resize. No black halo around text.

7. **Small comfort fixes (B12, B13, B16)**
   - Single instance: named `Mutex("Local\\WinternetMeter")`; second launch exits silently.
   - Startup: write `"\"" + Environment.ProcessPath + "\""`; on launch, if the Run entry exists but points elsewhere, rewrite it to the current path.
   - Units submenu: **MB/s (bytes)** / **Mbps (bits)**. Bits = bytes × 8, decimal (1000) prefixes — matches ISP/speed-test numbers.
   - Custom color → `ColorDialog`.
   - Tray icon tooltip stays `↓ x | ↑ y`.
   - ✅ Double-click the exe twice → one meter. Switch to Mbps → matches fast.com roughly.

8. **Releases (B2)**
   - `dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true` (+ `win-arm64`).
   - `.github/workflows/release.yml`: on tag `v*`, build both RIDs on `windows-latest`, attach `WinternetMeter-win-x64.exe` and `-win-arm64.exe` to the GitHub Release.
   - `installer/winternetmeter.iss` (Inno Setup): per-user install to `%LocalAppData%\Programs\WinternetMeter`, Start-menu shortcut, optional "Run at startup", uninstaller that removes the Run key. Built in the same workflow.
   - Note in README: unsigned exe → SmartScreen "More info → Run anyway" (signing is a later, paid step).
   - ✅ Fresh Windows 11 VM with no .NET installed: download → run → works.

9. **README rewrite**
   - Download link first, build instructions second. OS: Windows 10 (1809+) and 11, x64 + ARM64. Remove Win 7/8 claims.
   - Tip: Win 11 hides new tray icons — *Settings → Personalization → Taskbar → Other system tray icons → Winternet Meter: On*.
   - Move `demo2.gif` out of the repo (convert to a <2 MB GIF/WebP, or host it as a release asset / in a `docs` branch). Optionally purge history with `git filter-repo` — ask before rewriting history.

---

## Phase 2 — Extract the portable Core (no user-visible change)

Goal: all logic that isn't drawing pixels lives in `WinternetMeter.Core` with tests. Ship as **v1.2.0**.

1. Create `src/WinternetMeter.Core` (`net10.0`, no Windows APIs). Move the solution to the target layout.
2. **`SpeedSampler`** — input: a `Func<(long rx, long tx)>` counter source + a clock (inject `TimeProvider`). Output: bytes/sec. Handles first sample, counter reset, and zero elapsed time. Optional 3-sample smoothing (setting).
3. **`AdapterResolver`** — the Auto rules from Phase 1 task 3, platform-neutral; virtual-adapter name patterns include Linux ones too: `^(lo|docker\d*|br-|veth|virbr|vboxnet|tun|tap|wg|zt|tailscale)`. Raises `AdaptersChanged`.
4. **`SpeedFormatter`** — bytes vs bits, unit scaling, fixed width. Pure function.
5. **`AppSettings` + `JsonSettingsStore`** — path from `Environment.GetFolderPath(SpecialFolder.ApplicationData)` (gives `%AppData%` on Windows, `~/.config` on Linux). Atomic write (temp file + rename). Unknown/missing keys → defaults.
   - **One-time migration**: on first run of v1.2 on Windows, read old `Properties.Settings` values and write `settings.json`, then drop `Properties/` from the project.
6. **`IAutostart`** interface (`IsEnabled`, `Enable`, `Disable`); Windows impl (Run key) stays in the Windows project.
7. `tests/WinternetMeter.Core.Tests` (xUnit): sampler math with a fake clock, counter-reset case, formatter table tests, resolver filtering with fake adapter lists, settings round-trip + corrupt-file fallback.
8. `ci.yml`: build + test on `windows-latest` **and** `ubuntu-latest` (Core and tests only on Ubuntu). This is the proof the Core is portable.
   - ✅ Windows app behaves identically; `dotnet test` green on both OSes.

---

## Phase 3 — Ubuntu front-end (Avalonia)

Goal: same experience on Ubuntu 24.04+. Ship as **v2.0.0**.

### Platform facts to design around
- **Ubuntu uses Wayland by default.** Native Wayland clients can't place their own windows or force always-on-top. Avalonia's Linux backend runs on **X11, i.e. through XWayland** on Ubuntu, where position and keep-above are generally honored by GNOME — **verify early (task 2) before building the rest.**
- **Tray icons on GNOME** need AppIndicator/StatusNotifierItem. Ubuntu ships the *Ubuntu AppIndicators* extension enabled, so Avalonia's `TrayIcon` should show. Vanilla GNOME (Fedora etc.) won't — document it.
- `NetworkInterface.GetIPStatistics()` works on Linux (reads `/proc/net/dev`), so the Core needs no change.

### Tasks
1. `src/WinternetMeter.Linux` — Avalonia app referencing Core. Window: `SystemDecorations=None`, `Topmost=true`, `ShowInTaskbar=false`, `TransparencyLevelHint=Transparent`, `Background=Transparent`, rounded pill drawn in XAML.
2. **Spike first (half a day):** a bare transparent, topmost, draggable window on Ubuntu 24.04 GNOME Wayland session and an Xorg session. Record what works: position restore, keep-above, click-through-free drag (`BeginMoveDrag`), transparency. **If keep-above or positioning fails, stop and decide** between: (a) "normal window" mode on Wayland, or (b) adding the GNOME Shell top-bar extension (see Phase 4) as the primary Ubuntu UI.
3. `TrayIcon` with the same menu as Windows (Adapter / Units / Font / Size / Color / Fix position / Reset position / Run on startup / Exit). Build the menu from one shared description in Core (`MenuModel`) so both front-ends stay in sync.
4. **Autostart (Linux `IAutostart`)**: write/remove `~/.config/autostart/winternetmeter.desktop` (`Exec=` absolute path, `X-GNOME-Autostart-enabled=true`).
5. **Single instance**: lock file in `$XDG_RUNTIME_DIR/winternetmeter.lock` (`FileShare.None`).
6. **Network change**: `NetworkChange` events + re-resolve every 10 s as a fallback (cheap; NetworkManager churn is frequent).
7. **Fullscreen hide**: skip on Linux v2.0 (no portable API); keep the menu item Windows-only.
8. **Packaging** (in `release.yml`, `ubuntu-latest`):
   - `dotnet publish -r linux-x64 --self-contained -p:PublishSingleFile=true` (+ `linux-arm64`).
   - **`.deb`** via `nfpm` (installs to `/opt/winternetmeter`, `/usr/share/applications/winternetmeter.desktop`, icon in `/usr/share/icons/hicolor/256x256/apps/`).
   - **AppImage** as the no-install option.
   - ✅ Fresh Ubuntu 24.04 VM: `sudo apt install ./winternetmeter_2.0.0_amd64.deb` → launch from app grid → tray icon visible, widget floats, survives reboot with autostart on.

---

## Phase 4 — Optional polish (pick by need)

- **GNOME Shell top-bar extension** (`winternetmeter@rakibulx33`, GJS): reads `/proc/net/dev` directly, shows `↓ x ↑ y` in the panel. The most native Ubuntu experience and immune to Wayland window rules. Separate small codebase; publish on extensions.gnome.org.
- **Migrate Windows UI to Avalonia** once Linux reaches parity → one UI codebase. Only worth it if maintaining two front-ends hurts.
- **Dark-mode tray menu on Windows**: custom `ToolStripProfessionalRenderer` following `AppsUseLightTheme` from `HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize`.
- **winget manifest** (`winget install rakibulx33.WinternetMeter`) after a few stable releases.
- **Code signing** (removes SmartScreen warning) — costs money; skip until there are users.
- Per-app usage (which process is using bandwidth) — needs ETW on Windows / eBPF or `nethogs`-style on Linux; big scope, separate project.

---

## Definition of done (per release)

- [ ] `dotnet build` + `dotnet test` green in CI on Windows and Ubuntu
- [ ] Release artifacts attached by the workflow, not built by hand
- [ ] Manual check on a clean VM of the target OS (no SDK installed)
- [ ] Sleep/resume, Wi-Fi toggle, monitor unplug, second launch — all behave
- [ ] README download link and screenshots match the release
