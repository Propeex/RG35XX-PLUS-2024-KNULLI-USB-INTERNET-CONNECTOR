# Project context — read before every development session

## Mission

Build a Windows EXE (not a permanently open CMD/PowerShell window) that shares a Windows PC's internet with a KNULLI handheld over USB-C, without a Wi-Fi adapter. The first and only tested handheld is **RG35XX+ (2024, no Wi-Fi)**. The separate Wi-Fi-equipped RG35XX Plus has **not** been tested.

## Confirmed environment (2026-11-16)

- Device: ANBERNIC RG35XX+ (2024 edition); H700 ARM64; KNULLI Scarab build 2026-05-11; Linux 4.9.170. PortMaster detection may report `rg35xx-h`: do not use that string alone to determine retail model identity.
- Windows: ADB serial observed `0123456789`, root `adb shell`; these observations are diagnostic, **not hardcoded credentials or identifiers**.
- ADB `forward tcp:2222 tcp:22` works. ADB `reverse` did not work on this firmware.
- Reverse dynamic SSH forwarding `ssh -R 1080 -p 2222 root@127.0.0.1` makes a SOCKS endpoint at `127.0.0.1:1080` on the handheld.
- Linux supports built-in TUN (`CONFIG_TUN=y`), but creating a dummy network interface failed with `Operation not supported`.
- Legacy USB network: `rgusb0` TUN, `198.18.0.1/15`, default route through TUN, DoH DNS helper at `127.0.0.2` and tun2socks, with a watchdog. Native (no proxy options) HTTPS returned `HTTP/2 200`.
- The **v1.4 backend** proved native DNS+HTTPS in a separate ADB session. Earlier versions failed when detached helper processes died after ADB shell exit.
- ScreenScraper downloaded and applied metadata successfully after user fixed an independent ScreenScraper credential error.
- KNULLI network settings showed Internet CONNECTED but IP address NOT CONNECTED; this caused theme/content entry checks to fail. Verified workaround:
  `ip addr add 192.0.2.123/32 dev rgusb0 label rgusb0:eth0`.
  It made the IP visible and unlocked menus. The address is a local display alias, **not** internet reachability.
- PortMaster crashed at launch with `requests.exceptions.InvalidSchema: Missing dependencies for SOCKS support.` because globally inherited `ALL_PROXY/HTTP_PROXY/HTTPS_PROXY=socks5h://127.0.0.1:1080` but its bundled Python Requests lacked PySocks. Verified workaround: remove only those proxy environment variables in `/userdata/roms/ports/PortMaster.sh` (backing up original), allowing PortMaster startup, update and port installs via native TUN.
- Some content downloads returned HTTP 404; others worked. No claim that the network fixes missing remote content.
- Remaining historical issue: stale `active` flag plus DNS failure could prevent v1.5 startup, including after manual STOP. Windows `adb.exe` server persisted and could lock `platform-tools` folder after GUI/script closed.

## Known good vs unverified

**Proven:** Legacy v1.4 native internet, ScreenScraper, themes, PortMaster after compatibility fixes.

**NOT verified end-to-end:** Legacy v1.5.1 automatic stale recovery; legacy v1.6 PowerShell WinForms GUI; new C# EXE; automatic updates; cable-disconnect recovery; UDP/ICMP.

## Historical packaging

Earlier conversational ZIPs were produced outside this repository, including `RG35XX_2024_USB_Full_Internet_v1_4.zip`, `...v1_5_Integrated.zip`, `...v1_5_1_AutoRecovery.zip`, `...v1_6_GUI.zip`. Do not assume a future chat or CI runner can access these files. **Explicitly migrate and audit required source and binaries** before claiming a complete standalone release.

Legacy device scripts: `start-fullnet.sh`, `stop-fullnet.sh`, `watch-fullnet.sh`, `check-fullnet.sh`, `compat-fullnet.sh`, `setup-ssh-key.sh`.
Legacy helpers: ARM64 `tunctl`, `dns-bridge`, `daemon-launch`, and externally downloaded tun2socks. Confirm authorship, license, SHA-256 and reproducible build/source provenance before publishing these binaries.

## Constraints from testing

- Never restart EmulationStation from an ADB shell: previously caused indefinite "saving metadata" state and forced reboot.
- Avoid replacing native helpers while they run; STOP first, then validate `active` and `rgusb0` are gone.
- Stop only helper processes owned by this application; be transparent when terminating the shared ADB server.
- Keep per-user SSH private key under Windows user data. Never commit or log it.
- Make every device-side modification idempotent, backed up and reversible.
- Do not infer real connected status from a stale flag or from a UI icon alone. Check native DNS/HTTPS.
- Respect differences between normal TCP (tested) and UDP/ICMP (not guaranteed).

## User-approved product requirements

1. EXE opens a lightweight Windows GUI.
2. Startup app update from GitHub Releases, automatic apply and restart.
3. First launch auto-download latest official Windows Platform-Tools; subsequently compare version and update.
4. **Updated requirement (2026-11-16):** Progress bars only in a **first-run setup window**. Later launches do checks/updates before main screen without a progress bar; main screen never shows update progress.
5. Program name top-left; language chooser top-right; instant switch.
6. Translate only buttons and status messages; keep execution logs in English.
7. Limit first set of languages to Korean, English, Japanese.
8. Live device/connection status below title.
9. Exactly one prominent central Connect/Disconnect toggle.
10. Bottom English execution log.
11. Persist development objectives, design philosophy, current verified state, decisions, test results, and changelogs in this repository for future ChatGPT sessions.
12. Beta/pre-releases first; stable version only after actual reliability testing.

## How to resume in another chat

Give the assistant this GitHub repo URL, ask it to **read README.md, docs/PROJECT_CONTEXT.md, docs/ARCHITECTURE.md, docs/ROADMAP.md and docs/DEVELOPMENT_LOG.md**, inspect open PRs/issues/releases and latest commit, then implement the next highest-priority uncompleted issue on a branch/PR. Do not rely on memory of a previous conversation.

## Most recent approved UX change (2026-11-16)

- **Main UI must never display update progress bars.** Program and Platform-Tools update checks/installation finish before entering main UI.
- Display download progress **only on first-ever startup** in a dedicated startup window. Subsequent startups may briefly display a status message without progress bars.
- User explicitly asked to keep developing and log all changes in GitHub. The GUI foundation PR is #6. No hardware-ready EXE released yet.

## 2026-11-16 latest repository state (after PR #6)

- PR #6 merged into `main` as commit `dfa97ae63e597d965d8f8d683be5050edfaa8be8`.
- Windows CI run `43791370453` passed Build, Publish self-contained single-file EXE, and offline smoke tests. This is **not** a full functional release.
- Main EXE UI has no update progress bar. First-run-only startup BootstrapForm displays progress; later launches silently check/apply updates before MainForm.
- Google SDK Platform-Tools bootstrap/update and GitHub application updater have code in `main`; both need manual first-run/update tests.
- The internet Connect button remains intentionally **disabled** until issue #4 brings over the verified KNULLI backend. No GitHub Release exists yet.

## 2026-11-16 — Issue #4 transitional engine integration (PR #7)

- Original local v1.4 and v1.6 ZIPs were reopened and inspected, rather than relying on prior summaries. Verified actual legacy SHA-256 values: v1.4 `72f7b2b6f21932a27f742672fe389736b272fde84af6a1cfc1143135d87639cc`, v1.6 `3f69756af1825eb6738dca25e0c9222dc43d17991fbc051670fce7f5778d015d`.
- The new EXE can import only the pinned v1.6 ZIP from beside the EXE into a private app-data folder, and patches the script's ADB executable path to use officially managed Google Platform-Tools. No binary is committed.
- GUI single toggle starts/stops the legacy engine with no visible CMD/PowerShell window and checks native HTTPS. UI logs remain English; full legacy diagnostic logs stored separately.
- Windows ADB server is assigned port 5056 for the connector. GUI conservatively recognizes a preexisting owned SSH PID marker to avoid killing the tunnel on GUI restart. It can request Windows-side STOP even after USB unplug, but remote cleanup remains unverified offline.
- Original Go sources for tunctl, dns-bridge and daemon-launch and the KNULLI device shell scripts are now under native/. GitHub Actions tests and cross-compiles Go ARM64 tools. External tun2socks v2.7.0 pinned ZIP SHA-256 matches upstream GitHub asset.
- **Not complete:** migration of the Windows PowerShell source, full self-contained binary packaging, first-run/reconnect physical QA and release. See docs/BACKEND_TESTING.md.

- Added a startup safeguard: when the previous GUI's SSH PID marker remains, skip both EXE and Platform-Tools replacements until the user safely disconnects. This prevents startup failures caused by Windows file locks after choosing to keep internet running in background.

## Latest checkpoint for future chats (2026-11-16)

- PR #7 **merged into main** at commit `669f7da87fff8d3e04732f20b3507627435e0d3d`.
- Source under `native/` includes the three reproducible Go Linux ARM64 helpers and six KNULLI shell scripts, with native CI tests/builds passing.
- Main GUI has single Connect/Disconnect backed by hidden PowerShell using a **separately supplied SHA-pinned v1.6 legacy ZIP**. This is a transitional developer bridge, NOT a verified standalone connector.
- Bootstrap defers app and Platform-Tools updates if the app's prior SSH PID marker remains, to avoid modifying locked adb.exe during an active tunnel.
- Next priority: migrate Windows orchestrator and package audited helpers directly so no old ZIP is required; then user physical test and prerelease. Keep issue #4 open.

## Latest developer checkpoint: embedded backend (2026-11-16)

- The former `LegacyEngineBundle` manual v1.6 ZIP importer has been replaced with `UsbBackendBundle`, embedding the complete backend inside the EXE in CI. Users should **not** need to supply any historical v1.x ZIP for the new development build.
- Windows network orchestration now lives at `windows/USB-FullInternet.ps1`, derived from direct inspection of the old v1.6 script and preserving the tested v1.4 architecture. The new source has **not** undergone hardware testing.
- CI builds ARM64 helper binaries from `native/`, downloads verified tun2socks v2.7.0 with its upstream MIT license, builds the resource ZIP, parses PowerShell 5.1 code, creates a self-contained Windows EXE, and executes embedded-hash smoke tests.
- EXE extracts its embedded backend into a content-addressed directory under `%LOCALAPPDATA%`, never updating active helper files in place. Its hidden SSH/TUN/DNS orchestrator still uses app-specific ADB port 5056.
- Next work: manual first-run bootstrap, USB connection/stop/reconnect/PortMaster and GitHub self-update QA on real Windows + RG35XX+ (2024). Hardware tests NOT complete; no GitHub Release yet.


## Most recent repository checkpoint (2026-11-16, after PR #8)

- `main` includes CI-embedded single EXE backend; historical ZIP import has been removed.
- PR #8 merge commit: `b8e58e197b0505e90ad61d399cd0f54c041c9956`.
- Full passing CI run: `43796188343`. Its **development build** artifact is `gui-development-build`, not a release.
- Main action is still one Connect/Disconnect toggle; locale affects buttons/status; activity logs English; update progress **only on the first-run bootstrap screen** and never in main UI.
- Do not label the new EXE functional or stable until the user's physical Windows/RG35XX+ startup, network, STOP and PortMaster tests pass. Issue #4 remains open.
- Source-of-truth for future ChatGPT sessions: README, ARCHITECTURE, ROADMAP, DEVELOPMENT_LOG, BACKEND_TESTING, open issues and Releases.

## User's first actual GUI screenshot and confirmed requests (2026-11-16)

User tested GitHub Actions EXE on Windows. The English activity log confirmed **Google Platform-Tools 37.0.1 installed** and the **embedded backend verified**, yet status incorrectly said `Disconnected` while USB was physically connected. The screenshot also showed the top-right `Language` label clipped and the first-run Google SDK license prompt originally shown in English.

User corrected the delivery intent: **not a single EXE**. They want a **ZIP to extract into a folder, then run the EXE within it**, with official Google Platform-Tools installed into that same folder. This format makes scripts/settings inspectable and maintainable.

New approved UX: first-run language selection before Google Platform-Tools licensing/downloading; Korean, English and Japanese. Keep only progress in first-run setup, none in main. Fix clipped Language label. Fix false disconnected device detection.

Probable ADB false negative found in code: `AdbProbe.HasReadyDeviceAsync` required literal `"\\tdevice"`, whereas `adb devices -l` may produce spaces. A second potential cause is concurrent dedicated ADB 5056 vs working 5037. The new probe parses whitespace-delimited state, diagnoses offline/unauthorized, and falls back to shared 5037 while preserving it. **The hypothesis is code-based; await user hardware retest before claiming root cause.**

Portable split: `./backend/` checksum-verified scripts/binaries, `./platform-tools/` official Android SDK downloaded beside EXE, `./data/settings.json` chosen language, per-user sensitive SSH keys/logs under `%LOCALAPPDATA%`.

GitHub issue #4 remains open for device connection, native HTTPS, PortMaster, STOP and reconnect; issues #2 and #3 for official SDK/version update and complete portable-ZIP app updates. PR #9 implements the user's feedback, pending CI/hardware test.
