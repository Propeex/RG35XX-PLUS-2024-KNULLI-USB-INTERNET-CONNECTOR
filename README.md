# RG35XX+ (2024) KNULLI USB Internet Connector

**Status: experimental v0.1.0-beta.2 prerelease.** The user verified recognition and coexistence with ADB Explorer when the handheld USB ADB interface is enumerated; one native USB Internet connection was proven on an earlier development EXE. The exact beta.2 START → STOP → START, fresh-runtime setup and release-to-release update still need physical testing. **USB unplug/replug may require resetting USB ADB mode on the handheld** because Windows stops enumerating the USB device. This is not a stable release.

**Beta download:** [GitHub Releases — v0.1.0-beta.2](https://github.com/Propeex/RG35XX-PLUS-2024-KNULLI-USB-INTERNET-CONNECTOR/releases/tag/v0.1.0-beta.2). Extract the complete portable ZIP, then run **`KNULLI USB Link.exe`**. The official GitHub Release also provides a ZIP SHA-256 checksum.

A Windows desktop utility for sharing a PC's internet connection with the **Anbernic RG35XX+ (2024 edition, no built-in Wi-Fi)** running KNULLI, using a USB-C cable—without a Wi-Fi dongle.

> **Model naming:** The 2024 Wi-Fi-less RG35XX+ is not the separate Wi-Fi-equipped RG35XX Plus model. Only the Wi-Fi-less 2024 unit has been tested so far. Other H700 devices are not confirmed compatible.

**Shared ADB and USB unplug behavior:** The program uses the same standard port **5037** as ADB Explorer and never kills that shared ADB server on exit; an `adb.exe` background process remaining is normal. The program does **not** automatically connect/reconnect Internet, reset KNULLI USB mode, or patch firmware. When Windows does not enumerate the handheld after a cable replug, users may need to reselect KNULLI's existing USB ADB mode. See [Korean installation/recovery notes](docs/PORTABLE_INSTALL_KR.md).

## Project goals

- Launch a **Windows EXE** and show a lightweight native GUI.
- Check **GitHub Releases** on launch, safely update the application, then restart.
- Download the official **Android SDK Platform-Tools for Windows** on first launch; check and update subsequent installations.
- Show update/download progress **only during first-time setup in a separate startup window**. On all later launches check/apply updates **before** entering the main UI, without any progress bars in the main UI.
- Display the application title at upper left and a language selector at upper right.
- Translate only **buttons and status labels**; keep diagnostic logs in **English**.
- Show continuously updated USB/device/internet connection status beneath the title.
- Provide **one central Connect / Disconnect toggle button**.
- Show a scrolling **English activity log** at the bottom.
- Recover stale network state safely, without restarting EmulationStation or overwriting active helpers.

**Planned UI languages:** Korean (ko-KR), English (en-US), Japanese (ja-JP). Switching should apply immediately.

## What is already proven?

The legacy **v1.4 USB network backend** was tested successfully on RG35XX+ (2024) with KNULLI Scarab (2026-05-11): ADB, SSH reverse SOCKS tunnel, TUN/tun2socks, DNS, and native HTTPS. ScreenScraper, theme downloads, and PortMaster updates/installs also worked after device-side compatibility fixes.

**Important:** The later legacy v1.5–v1.6 ZIP prototypes were *not* fully verified end to end on the physical device. On 2026-11-16 the user reported a successful full Internet connection using the development EXE after adding KNULLI default-password SSH fallback. This is one positive hardware observation, **not** repeatable START/STOP, status-indicator, or exit-cleanup validation.

## Development & continuity

Read these files **before making changes**, especially when continuing in a new ChatGPT conversation:

1. [docs/PROJECT_CONTEXT.md](docs/PROJECT_CONTEXT.md) — original goal, confirmed tests, device quirks, failed approaches.
2. [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) — architecture and security decisions.
3. [docs/ROADMAP.md](docs/ROADMAP.md) — development phases, acceptance criteria.
4. [docs/DEVELOPMENT_LOG.md](docs/DEVELOPMENT_LOG.md) — chronological decisions and changes.

All code changes should be visible in GitHub commits or PRs, and each tested milestone should have a versioned **GitHub prerelease**. Stable `v1.0.0` is reserved for a validated, reliable release. Publishing is **not** the same as testing.

## Safety commitments

- No credentials, SSH private keys, local logs, or user-specific data committed to Git.
- Verify downloaded files (at least integrity hashes; prefer code signing for production).
- Download Platform-Tools from Google's official Android SDK distribution.
- Do not modify unrelated ADB sessions or processes without warning.
- Keep working KNULLI network and PortMaster settings backed up and reversible.
- Preserve successful behavior before optimizing the UX.

## Source status

Current source includes the **tracked Windows PowerShell orchestrator**, KNULLI shell scripts, and reproducible Go ARM64 helpers. GitHub Actions assembles a SHA-256 verified `backend/` directory with upstream MIT-licensed tun2socks v2.7.0 and packages it **beside a portable Windows EXE in a ZIP**. CI checks source syntax, hashes and build integrity. **Do not advertise it as fully working until real-device tests pass.**

## License

To be determined after auditing the legacy backend and bundled dependencies. Do not assume third-party binaries can be redistributed.

### Startup and update UI policy (approved 2026-11-16)

1. First launch: a **separate startup/setup screen** may show real download progress for the application and Google's Platform-Tools. Once setup finishes, open the main UI.
2. Subsequent launches: automatically check/apply updates **before** opening the main UI; a minimal status-only startup window is allowed, but show **no progress bars**.
3. **Never include application/SDK update progress controls in the main interface.** Manual USB Internet START/STOP now has a separate progress bar showing completed backend stages while the operation runs, as requested by the user.
4. As of the merged GUI foundation (PR #6), self-update and official SDK installation code **build and pass offline smoke tests in Windows CI, but require fresh-Windows and real-device validation**, and the USB network engine is still pending. Do not mistake a development build artifact for a release.

## Portable folder distribution — development build

Following user hardware feedback, the delivery format is **a small ZIP of a folder**, not a lone self-contained EXE. Download the newest successful `windows-portable-zip` build, extract the inner ZIP, then launch **`KNULLI USB Link.exe`**. A small Windows .NET Framework 4.x launcher selects language and, only when missing, asks permission to install Microsoft's .NET 8 Desktop Runtime once for the current user. The internal `RG35XXUsbInternetConnector.exe` is framework-dependent; `backend/` remains readable and hash-verified. Do not directly launch the internal EXE on machines lacking .NET 8.

The EXE automatically installs **Google's official Android SDK Platform-Tools into `platform-tools/` beside itself**, not into a hidden global folder. The first-run setup asks for language (Korean, English, Japanese) **before** showing a localized Google SDK license dialog. Language and setup markers are saved in `data/`, while SSH private keys and sensitive logs remain private in the Windows user's `%LOCALAPPDATA%`. The main GUI continues to have one Connect/Disconnect button and no update progress bar.

The beta.2 development branch now uses **one standard ADB server on port 5037**, shared with ADB Explorer regardless of application launch order. Passive `host:devices-l` checks and `host:track-devices` notifications no longer start competing servers during polling. A genuinely active session created by a legacy build on port 5056 remains pinned only for explicit disconnect. The app never terminates shared 5037 and never automatically connects USB Internet. **The USB-first and ADB Explorer launch-order fixes require physical re-testing before beta.2 release.**

Future GitHub Release application updates must provide `RG35XX-USB-Internet-Connector.zip`, containing the launcher, framework-dependent EXE and backend. The updater stages/verifies and swaps the set together while preserving `platform-tools/` and `data/`.

See [portable Korean install guide](docs/PORTABLE_INSTALL_KR.md) and [real device QA](docs/BACKEND_TESTING.md). The developer build has passed several CI stages but is **not a verified hardware-ready Beta Release**.
