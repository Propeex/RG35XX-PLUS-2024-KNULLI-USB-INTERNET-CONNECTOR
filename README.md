# RG35XX+ (2024) KNULLI USB Internet Connector

**Status: early development / beta preparation. No tested EXE release yet.**

A Windows desktop utility for sharing a PC's internet connection with the **Anbernic RG35XX+ (2024 edition, no built-in Wi-Fi)** running KNULLI, using a USB-C cable—without a Wi-Fi dongle.

> **Model naming:** The 2024 Wi-Fi-less RG35XX+ is not the separate Wi-Fi-equipped RG35XX Plus model. Only the Wi-Fi-less 2024 unit has been tested so far. Other H700 devices are not confirmed compatible.

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

**Important:** The later legacy v1.5–v1.6 ZIP prototypes were *not* fully verified end to end on the physical device. The new EXE is a fresh, tracked implementation and is **not yet released or claimed to work**.

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

Current source includes the **tracked Windows PowerShell orchestrator**, KNULLI shell scripts, and reproducible Go ARM64 helpers. GitHub Actions assembles a SHA-256 verified backend with upstream MIT-licensed tun2socks v2.7.0, embeds it in a self-contained Windows EXE, and runs offline build/syntax/integrity checks. **Do not advertise it as fully working until real-device tests pass.**

## License

To be determined after auditing the legacy backend and bundled dependencies. Do not assume third-party binaries can be redistributed.

### Startup and update UI policy (approved 2026-11-16)

1. First launch: a **separate startup/setup screen** may show real download progress for the application and Google's Platform-Tools. Once setup finishes, open the main UI.
2. Subsequent launches: automatically check/apply updates **before** opening the main UI; a minimal status-only startup window is allowed, but show **no progress bars**.
3. **Never include any update progress controls in the main interface.** The main interface contains the title, language selector, real-time connection status, one central connect/disconnect button and an English log only.
4. As of the merged GUI foundation (PR #6), self-update and official SDK installation code **build and pass offline smoke tests in Windows CI, but require fresh-Windows and real-device validation**, and the USB network engine is still pending. Do not mistake a development build artifact for a release.

## Embedded USB backend — developer build only

Issue #4 has progressed beyond the earlier manual legacy ZIP importer. The new EXE carries a complete, checksum-verified network payload assembled by CI; **no v1.4/v1.6 ZIP needs to be copied alongside the program**. Windows PowerShell orchestration is tracked in [windows/USB-FullInternet.ps1](windows/USB-FullInternet.ps1). ARM64 device helpers and scripts are under [native/](native/); xjasonlyu/tun2socks v2.7.0 is pinned and its MIT license is preserved under [third_party/](third_party/).

The GUI has one central Connect/Disconnect action, requires native HTTPS to display Connected, and has no update progress in the main window. All update/setup work happens before main GUI, with a progress bar only on first run.

**Hardware validation is still pending.** Use the [development build/testing guide](docs/BACKEND_TESTING.md). Do not publish a user-facing beta Release until the real RG35XX+ (2024) test and update-restart tests pass.
