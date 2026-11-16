# Development log (append-only chronological notes)

## 2026-11-16 — Repository initialized
- Confirmed repository `Propeex/RG35XX-PLUS-2024-KNULLI-USB-INTERNET-CONNECTOR` existed and was initially **empty**.
- Development mandate: GitHub is the source of truth; future chats read repo docs/issues/PRs before coding. Maintain full history and explicit test evidence.
- Chosen direction: Windows lightweight GUI EXE; integrated app/Google Platform-Tools updates; visible progress; language menu; live device status; one Connect/Disconnect button; English log.
- Early beta policy; do **not** label historical ZIPs as published GitHub releases. No stable release without physical testing.
- First action: architecture, support scope, roadmap, project context and transparent README.
- Pending: recover/audit legacy source and native helpers; write/compile EXE; verify on Windows and RG35XX hardware.

## Historical findings (pre-repository, summarized)
- KNULLI v1.4 USB TUN/DNS successfully reached external HTTPS without proxy flags. Earlier builds lost helper processes after exiting ADB.
- KNULLI IP UI was NOT CONNECTED despite a working TUN. `rgusb0:eth0` secondary IP label fixed access to downloads.
- PortMaster Python Requests lacked SOCKS support; unsetting inherited proxy variables **only in its launcher** fixed startup, online update and installations.
- Some content downloads failed HTTP 404 but others worked.
- v1.5 auto-recovery was blocked by stale `active` / DNS failure; Windows ADB server could linger and lock the tools folder.
- v1.6 historical script-based WinForms GUI was proposed, but **not yet verified as a working EXE**.
- Legacy build artifacts were outside the GitHub repository. Do not assume they are present in a fresh chat or checkout.

## 2026-11-16 — Updated startup UX and first EXE implementation (PR #6)
- User explicitly removed update progress from the main GUI. Only **first-run separate setup** may display progress; updates on subsequent launches occur **before** main screen opens, without progress bars.
- Refactored Program to launch BootstrapForm before MainForm; removed two update bars from MainForm.
- Added SDK license acknowledgement, first-run marker and retry-on-failure logic.
- Implemented official Google SDK repository stable metadata inspection, staged Platform-Tools Windows ZIP download, SHA-256 verification when provided, safe archive validation, version comparison, swap with recovery and offline fallback.
- Implemented beta/stable GitHub Releases detection, SemVer comparison, expected single-file EXE asset and SHA-256 validation, and updater copied from the running EXE to avoid Windows file locking.
- Added real ADB USB readiness polling via isolated short-lived ADB server on port 5056, distinguishing device-ready status from internet-connected status. Network toggle is deliberately disabled until engine #4 is migrated.
- Added self-contained single-file publish setting. Windows CI/real hardware testing still required; no release was created.
- Sources live in `feature/windows-gui-foundation`, linked to draft PR #6. Main documentation records decisions to allow future chats to resume.

## 2026-11-16 — GUI foundation CI validated and merged
- GitHub Actions run `43791370453` successfully restored, built, published a self-contained single-file Windows EXE and passed offline smoke tests.
- Fixed one C# updater launch expression after an early CI compiler failure and adjusted CI to wait correctly for a WinExe process before checking its exit code.
- PR #6 was squash-merged into main at `dfa97ae63e597d965d8f8d683be5050edfaa8be8`.
- **No GitHub Release was created:** the actual USB networking backend is not integrated, and the new updater and first-run installer are not verified on a physical Windows/RG35XX system. The one central Connect/Disconnect button is intentionally disabled for this development foundation.
- Next priority: audited migration of legacy v1.4 backend (issue #4) and fresh-machine GUI/update testing (issues #2/#3); then a tested beta prerelease.

## 2026-11-16 — Real legacy engine bridge and native source migration (PR #7)

- Reopened exact locally available v1.4/v1.6 ZIP source and copied their historical behavior into the GitHub issue #4 implementation plan.
- Added `LegacyEngineBundle` with pinned v1.6 SHA-256 import from alongside the developer EXE. No legacy native binaries are checked into public GitHub or distributed through release assets.
- Added `LegacyEngineRunner` to run the original backend invisibly from the WinForms toggle, on an app-specific ADB server port 5056, with English UI logs and separate legacy logs. Report Connected only after independent native HTTPS test.
- Changed GUI to allow a conservative Windows cleanup request after unplug and avoid killing the owned SSH session when GUI reopens. The existing PortMaster proxy fix, KNULLI IP alias, and network rollback are preserved by the older device scripts pending validation.
- Migrated helper Go sources and tests, plus KNULLI shell scripts to `native/`; added native reproducible ARM64 build and shell-syntax CI.
- Verified upstream xjasonlyu/tun2socks v2.7.0 release asset SHA-256 equals the historical pinned value `3931476c9cfa8fa236d23aeaf36767df0eb27cc11ecaab699faba57744450f49`. Upstream reports MIT license.
- Updated `docs/BACKEND_TESTING.md` for a reproducible hardware test. **No functional EXE Beta Release** until this path works on real Windows/handheld hardware and fully audited native packaging is ready.

- Startup now defers both automatic EXE update and Google Platform-Tools replacement when the app-owned SSH PID marker exists, avoiding in-use adb.exe replacement and reconnect loops. This is intentional even if the PID marker might be stale; user must use STOP before updating.
