# Architecture and engineering principles

## Core principle

**A visually simple app on top of a testable, reversible network engine.** Keep UI/translation separate from USB/SSH/TUN implementation and update/install routines. No network engine rewrites merely to simplify the interface.

## Intended technology

- Windows 10/11 x64, .NET 8 **WinForms** desktop app (single EXE, self-contained publishing preferred so users do not install a runtime); evaluate distribution size later.
- Built-in `HttpClient` for official downloads and GitHub Releases metadata; `System.IO.Compression` for ZIP.
- An application-managed Platform-Tools directory under `%LOCALAPPDATA%` rather than requiring a manual copy into `platform-tools`. An existing local `adb.exe` may be offered as an advanced override, but the default must be zero-setup.
- Background operations async; GUI remains responsive; cancellation/recovery must not leave TUN/route half-configured.
- Native Windows UI (not an embedded browser). Status/button translations only; logs always English.

## UI contract

```text
+-----------------------------------------------------------+
| RG35XX+ (2024) KNULLI USB Internet Connector     Language v|
| Status: device disconnected / ready / connected / error   |
|                                                           |
|                  [ Connect / Disconnect ]                 |
|                                                           |
| Activity log (English)                                    |
| ...                                                       |
+-----------------------------------------------------------+
```

Languages: ko-KR, en-US, ja-JP. Language selection updates UI immediately and is persisted under user settings; log content is **not translated**.

## Update pipeline

1. Check latest compatible release using **GitHub Releases API**. GitHub `/releases/latest` omits prereleases; use the releases listing during beta. Compare proper semantic versions, prefer latest applicable channel (beta in beta builds, stable only in stable builds unless opted in).
2. Retrieve release metadata/asset and its **SHA-256 digest**; download to an app-owned temporary path with progress. Reject empty/incorrect size, unexpected asset name, failed hash, or untrusted redirect target. Consider Authenticode signing for production, as a repository-published checksum alone does not protect against a compromised release.
3. Never overwrite the running EXE. Use a narrowly scoped, signed/validated updater handoff: wait for EXE exit, back up old version, replace atomically, restart once, roll back on failure, avoid restart loops. Do not silently update when application network operations are active.
4. On a first install or after failed update, keep the last functioning application usable and give a meaningful English log.
5. Platform-Tools: fetch latest official Windows package **only from Google**, determine installed revision from `platform-tools/source.properties`, compare with Google's SDK repository metadata, download ZIP with progress, validate ZIP extraction paths and package structure, stage replacement, then switch atomically when no ADB operation is running.
6. Installed Platform-Tools location must be stable and separate from app EXE to avoid Windows file locking. Avoid an endless `adb devices` loop that restarts the ADB daemon immediately after Disconnect.
7. **New startup UX rule:** only first-run setup has a separate progress screen, with real determinate download % and indeterminate checks/verifications. On subsequent launches, show at most a status-only startup window. Never show update progress inside the main screen. Keep main UI hidden until checks/updates finish.

## USB connectivity pipeline

- Discover ADB device; require **exactly one** ready device and root shell, unless future multiple-device selection is supported.
- ADB forward `tcp:2222` -> device SSH 22. Use app-specific Windows SSH key, registered on device via ADB, stored in the current Windows user's private application data.
- Run SSH in background with strict PID+command ownership checks; do not kill unrelated SSH processes.
- Launch TUN/tun2socks and DNS helpers on KNULLI with a daemon/session launcher after verifying old helpers stopped, and use native HTTPS/DNS for success criterion.
- Apply KNULLI IP label `192.0.2.123/32` to `rgusb0` only if absent.
- PortMaster compatibility: idempotently unset SOCKS proxy variables **inside PortMaster launcher only**, backing up the existing script; never disable needed proxy support globally if TUN is not working.
- Explicit Disconnect should gracefully stop device helpers, restore route/resolver if owned, remove temporary IP/flag, stop only owned SSH, and **optionally** kill shared ADB server after warning. Avoid restarting ADB immediately by idle status polling.
- Use a defined connection state machine, not just existence of a flag:
  `DISCONNECTED -> DISCOVERING -> CONNECTING -> CONNECTED -> DISCONNECTING`, with `RECOVERING / ERROR` branches.
- ADB may be running for unrelated applications; process-ownership rules are essential. A background status checker must not strand ADB after exit.

## Security/quality gates

- Do not store secrets in Git, logs, exceptions, telemetry, or support archives.
- Prefer no automatic telemetry. Any diagnostics export must redact private paths, SSH material, and identifying serial numbers.
- Include versioned backups and restore mechanisms for KNULLI modifications.
- CI must compile Windows EXE and run unit tests; production release requires physical RG35XX 2024 testing.
- Source and license audit required before distributing compiled ARM64 TUN/DNS helper binaries and third-party artifacts.
- Code review via PRs; accepted milestone -> version tag -> GitHub prerelease while in beta. Never claim a release exists before the GitHub API confirms it.

## Bootstrap/main window boundary (2026-11-16)

- `Program` first starts `BootstrapForm`, never `MainForm` directly.
- First run is detected via a marker in `%LOCALAPPDATA%\RG35XXUsbInternetConnector`; first-run progress only appears while the marker is absent.
- Main screen is created only after the bootstrap succeeds (or safely falls back to installed dependencies while offline).
- A future app update may launch a short-lived helper copy and exit the old process; the new version restarts and repeats the pre-main check.
- Google SDK license consent is collected before first automatic Platform-Tools download.
- As of this commit, source is **unverified on Windows**; all updater behavior requires CI + release-to-release tests before a beta release.

## Experimental bridge to original network engine (PR #7)

- `LegacyEngineBundle` only imports one exact SHA-256 pinned **user-supplied** historical v1.6 ZIP; it refuses unexpected content, unsafe paths and an unrecognized ADB path patch. There is no GitHub or other download of this old ZIP.
- `LegacyEngineRunner` uses a hidden Windows PowerShell process while preserving the historical ADB/SSH/TUN/DNS scripts. All actions have time limits and English high-level logging; original diagnostic files remain separate.
- The native health check runs an independent ADB curl with `--noproxy '*'` rather than checking only the `active` marker. GUI health probes do not kill the dedicated ADB server when an owned SSH tunnel may be active.
- **USB unplug is not equivalent to successful remote STOP.** The GUI must not claim KNULLI was restored if the device was unreachable. A Windows-only cleanup attempt is allowed.
- Source in `native/` is reproducible with Linux ARM64 Go CI; the full public release must eliminate manual legacy ZIP import by migrating/auditing the Windows orchestration and packaging verified helper artifacts.
- CI compilation is not hardware validation; keep PR #7 under development until physical tests are recorded.
