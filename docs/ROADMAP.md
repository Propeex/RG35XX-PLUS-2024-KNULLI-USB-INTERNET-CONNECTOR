# Roadmap and acceptance criteria

Current phase: **beta preparation**. Existing v1.4/v1.6 ZIP names are historical prototypes, not GitHub versions.

## Phase 0 — Repository foundation
- [x] Write public README and accurate support scope
- [x] Record real-device findings and previous failures
- [x] Specify app architecture, UI contract, update strategy, security and release policy
- [ ] Audit legacy helper licenses and migrate reproducible backend source
- [ ] Add a Windows desktop source project and CI build

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
- [ ] **Windows CI compile must pass; manual Windows startup tests required.**
- [ ] Verify real SDK repository XML, official ZIP checksums/structure on fresh Windows machine.
- [ ] Verify GitHub prerelease-to-prerelease update, restart and failure rollback.
- [ ] Integrate and test known-working v1.4 USB networking engine.
- [ ] Release only after tested milestone with appropriate beta designation.
