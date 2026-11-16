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
