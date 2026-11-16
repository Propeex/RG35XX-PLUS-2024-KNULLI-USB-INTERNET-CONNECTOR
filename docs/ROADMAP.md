# Roadmap and acceptance criteria

Current phase: **beta preparation**. Existing v1.4/v1.6 ZIP names are historical prototypes, not GitHub versions.

## Phase 0 — Repository foundation
- [x] Write public README and accurate support scope
- [x] Record real-device findings and previous failures
- [x] Specify app architecture, UI contract, update strategy, security and release policy
- [x] Migrate tracked Windows/Go/shell sources, preserve tun2socks v2.7.0 MIT notice, and pin upstream SHA-256 (CI; hardware test pending)
- [x] Add a Windows desktop source project and CI build (PR #6, CI verified; not hardware tested)

## Phase 1 — Working Windows GUI skeleton (development branch; CI pending)
- [ ] Launch from EXE; no persistent console
- [ ] App title upper-left, language selector upper-right
- [ ] Languages: Korean, English, Japanese; instant switching for **buttons/status only**
- [ ] Real-time, correctly modeled device/connection status beneath title
- [ ] One central Connect/Disconnect toggle
- [ ] English log panel at bottom
- [ ] Safe UI threading, no blocking network calls on UI thread

## Phase 2 — Automatic dependencies and app updater
- [ ] Detect app updates via GitHub Releases (including beta channel)
- [ ] Download, verify, stage, replace, restart and rollback safely
- [ ] Verify/update official Google Platform-Tools on first and subsequent launches
- [ ] Show accurate progress **only in first-run setup**; subsequent updates complete before main UI with no progress bars
- [ ] Work offline with already installed versions
- [ ] Do not respawn `adb.exe` after explicit Disconnect

## Phase 3 — Verified KNULLI USB internet engine
- [ ] Migrate/audit the known-good v1.4 backend and device scripts
- [ ] Package or securely install licensed dependencies
- [ ] Start and stop SSH/ADB/TUN/DNS cleanly; stale-state recovery
- [ ] Apply IP recognition and PortMaster fixes idempotently, with backups
- [ ] Physically test native HTTPS, ScreenScraper, theme installation, PortMaster update/install
- [ ] Test cable disconnect/reconnect, app exit, power cycle, error recovery and cleanup
- [ ] Verify no unnecessary `adb.exe` or `ssh.exe` remains when fully stopped

## Phase 4 — First beta GitHub Release
- [ ] CI Windows build and test pass
- [ ] Security/license audit complete
- [ ] Physical device test report attached to milestone
- [ ] Tag a version such as `v0.1.0-beta.1`
- [ ] Publish EXE, SHA256 checksum, changelog as a GitHub **prerelease**
- [ ] Confirm application can detect/install the subsequent prerelease

## Stable milestone (not yet scheduled)
- [ ] Repeatable connection/reconnection on the actual RG35XX+ (2024)
- [ ] No blocking crashes, orphaned process leaks, or unexpected system changes
- [ ] Proven auto-update/rollback behavior
- [ ] Tested fresh-user install and recoverable failures
- [ ] User signs off on stability -> `v1.0.0` stable release

## Release rules

- Every **completed, tested milestone** gets a semantic version, changelog and Release entry.
- During testing, mark releases as **prerelease/beta**. Do not confuse historical ZIP versions (v1.4-v1.6) with new EXE release numbering.
- Every change must be committed; prefer feature branches + reviewed PRs. Open issues for incomplete work and regressions.
- A working source commit is not a published release. Do not claim binaries exist until GitHub Actions actually produces them.

## Current implementation status (2026-11-16, PR #6)

- [x] Source refactor: remove all update/progress UI from `MainForm`.
- [x] Source: first-run-only `BootstrapForm` progress before opening `MainForm`.
- [x] Source: Google Platform-Tools metadata/version check, staged download and replacement.
- [x] Source: GitHub Release discovery, digest verification and separate EXE updater-copy handoff.
- [x] Source: ADB device readiness polling with isolated temporary server port.
- [x] Windows CI compile, single EXE publish and offline smoke tests passed (run 43791370453).
- [ ] **Manual Windows startup/update and actual RG35XX device tests required.**
- [ ] Verify real SDK repository XML, official ZIP checksums/structure on fresh Windows machine.
- [ ] Verify GitHub prerelease-to-prerelease update, restart and failure rollback.
- [ ] Integrate and test known-working v1.4 USB networking engine.
- [ ] Release only after tested milestone with appropriate beta designation.

## Issue #4 source migration snapshot (PR #7, 2026-11-16)

- [x] Pin and inspect the exact legacy v1.4 and v1.6 ZIPs and document their provenance.
- [x] Add conditional, SHA-256 checked **local** legacy backend import for device testing (not public redistribution).
- [x] Hook the one-button GUI to hidden PowerShell Start/Stop, native HTTPS verification and conservative status transitions.
- [x] Separate owned ADB port 5056 from unrelated applications; preserve active tunnel during UI polling/reopen.
- [x] Migrate Go native helper sources, tests, and KNULLI device shell scripts to `native/`.
- [x] GitHub Actions Linux ARM64 helper cross-build/test and shell syntax validation.
- [x] Track a Windows PowerShell 5.1 orchestrator under windows/ and remove the historical ZIP importer.
- [x] Build ARM64 helpers and hash-pinned MIT tun2socks in CI; embed a checksummed backend in the single EXE (unsigned developer build).
- [ ] Test the new EXE start/stop, SSH key setup, native DNS, ScreenScraper, theme and PortMaster on the physical device.
- [ ] Test unplug/reconnect, stale active flag, Windows process cleanup and recovery on the real device.
- [ ] Merge an acceptable milestone; create a **beta** Release only when actual functionality and update installer are tested.

### Merged development checkpoint

- [x] PR #7 merged into main (`669f7da`): hidden GUI-to-backend bridge, native Go/shell source and CI.
- [ ] No successful physical device test on the new EXE yet; EXE now embeds the backend without a historical ZIP (not yet physically validated). **Do not publish beta as working until that dependency is eliminated and tests pass.**

## Embedded backend milestone (branch feature/embedded-usb-engine)

- [x] CI builds three Go ARM64 helpers and checks POSIX device scripts.
- [x] CI downloads xjasonlyu/tun2socks v2.7.0, checks the published SHA-256 and includes its MIT notice.
- [x] CI parses the tracked Windows PowerShell 5.1 orchestrator.
- [x] Embedded ZIP verified with per-file hashes and extracted to versioned user-data folder; no manual legacy ZIP.
- [x] Single Windows EXE build and offline embedded-backend smoke test passed in GitHub Actions.
- [ ] Physical Windows 10/11 + RG35XX+ (2024) start/stop/PortMaster and stale-state QA.
- [ ] Test first-run official Google Platform-Tools bootstrap, EXE self-update/restart/rollback.
- [ ] Independent security/ownership review and code signing before stable public release.


## Latest PR #8 checkpoint

- [x] PR #8 embedded backend merged into `main` (`b8e58e197b`), CI build/syntax/SHA256 tests passed.
- [x] No external historical ZIP required for developer EXE.
- [ ] Real Windows 10/11 + RG35XX+ (2024) first-run and USB connection/disconnection verification.
- [ ] GitHub self-update and Platform-Tools update/restart regression testing.
- [ ] Publish `v0.1.0-beta.1` or later only after those checks pass.

## UX and packaging changes from first user GUI test — PR #9

- [x] Source: change delivery to folder ZIP with EXE + verified sibling `backend/`.
- [x] Source: install official Google `platform-tools/` alongside the EXE using same-volume staging.
- [x] Source: first launch selects language before localized SDK license consent; persist KO/EN/JA in portable `data/settings.json`.
- [x] Source: widen title row; prevent the `Language` label from clipping.
- [x] Source: ADB parser supports spaces/tabs; fallback from dedicated 5056 to shared 5037, suppress serials in English activity logs.
- [x] Source: app updater now expects a portable Release ZIP, verifies/stages EXE+backend together, preserves Platform-Tools/settings and provides rollback on failure.
- [ ] Windows CI validate new folder artifact, complete portable ZIP, local archive integrity, updater staging; tests must be **green** before merge.
- [ ] Re-test user's physically attached RG35XX+ 2024 to verify that ADB now reports `Ready` and connection button enables.
- [ ] Actually test updating from one portable GitHub beta release to the next including rollback, without deleting `platform-tools`.
- [ ] Verify USB native HTTPS, KNULLI IP display, PortMaster and STOP on physical device before beta Release.


## PR #9 verified source milestone

- [x] PR #9 merged into main (`5e2a7724a86f`).
- [x] GitHub Actions run `43800558352`: full ARM64, PowerShell, EXE, ADB parsing, folder ZIP and updater ZIP integrity checks passed.
- [x] Verified Actions artifacts `gui-development-build` and `windows-portable-zip`.
- [ ] **User hardware retest**: attached RG35XX+ (2024) must change from Disconnected to Ready before pressing Connect.
- [ ] **Manual Windows UI retest**: actual language selector, localized Google SDK consent and unclipped Language label.
- [ ] **Release update test**: portable EXE/backend upgrade across two prereleases while keeping adjacent Platform-Tools and user settings.
- [ ] **No beta Release yet**: wait for successful physical connection and reliable STOP.


## Next hardware retest — separate device/Internet indicators and START/STOP markers

- [x] Source: initial language chooser contains only `Language` and choices; selected locale applies before SDK consent.
- [x] Source: separate device and Internet binary indicators; device unplug hides Internet.
- [x] Source: no recycled startup messages in the main activity view, localized heading, current-language selector.
- [x] Source: ADB USB arrival/removal hints + frequent probe and multi-device rejection.
- [x] Source: explicit remote shell state markers, independent STOP checks and native HTTPS proof; live stage output during long actions.
- [ ] CI compile, PowerShell AST parse, ARM64 backend tests, EXE smoke and portable ZIP integrity **must pass** on this branch.
- [ ] Hardware: validate improved device plug/unplug reporting and both UI colors.
- [ ] Hardware: check actual `active`/TUN result, START, healthy HTTPS, successful STOP, reconnect and PortMaster.
- [ ] Regression: first-run three locales, persistent language, restart resets GUI activity log but retains private technical diagnostics.
- [ ] No Beta Release until these hardware and real portable update tests pass.
