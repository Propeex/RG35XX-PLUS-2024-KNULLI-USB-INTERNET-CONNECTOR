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

## 2026-11-16 — PR #7 merged to main (experimental backend milestone)
- PR #7 merged as `669f7da87fff8d3e04732f20b3507627435e0d3d` after Windows build/smoke tests and native Go/shell CI passed.
- The EXE source now has a functional **developer/test-only** hidden PowerShell Connect/Disconnect path when the exact local legacy ZIP is supplied. This has NOT been tested on the handheld and is not self-contained yet.
- Issue #4 stays open for audited packaging, source migration of Windows orchestration and real-device tests. No Beta Release/tag was created.

## 2026-11-16 — Replace old ZIP with embedded, source-tracked backend

- Reopened historical `RG35XX_2024_USB_Full_Internet_v1_6_GUI.zip` and `USB-FullInternet.ps1`, checked exact archive SHA, and used it as reference for a new English-logging PowerShell orchestrator at `windows/USB-FullInternet.ps1`.
- Added `UsbBackendBundle` and removed the old `LegacyEngineBundle` manual ZIP import; renamed runner to `UsbEngineRunner`.
- Added backend assembly job to Windows CI: Go ARM64 builds, shell tests, pinned upstream tun2socks release SHA verification, MIT notice, checksum manifest, embedded EXE resource, Powershell 5.1 parser validation, EXE publish and offline embedded-payload integrity tests.
- Runtime backend extraction now stages content under a SHA-256 based version directory in LocalAppData and refuses to overwrite corrupted or potentially active files.
- Original UI policy preserved: no update progress in main window; first-run setup progress only; program title, KO/EN/JA status/button translations, single connect/disconnect button, English logs.
- Completed GitHub Actions run `43795735996` passed ARM64 packaging, Windows PowerShell 5.1 syntax, Windows EXE compilation/publish and offline embedded-file smoke tests. **Physical RG35XX and update/restart tests are still outstanding.**
- Updated `docs/BACKEND_TESTING.md` for a test EXE without any old ZIP, and retained legacy historical milestones in the log for future context.
- No GitHub Release/tag created until physical-user acceptance tests pass.


## 2026-11-16 — Embedded engine source milestone merged

- PR #8 ([link](../pull/8)) squash-merged to `main`: `b8e58e197b0505e90ad61d399cd0f54c041c9956`.
- GitHub Actions [run 43796188343](https://github.com/Propeex/RG35XX-PLUS-2024-KNULLI-USB-INTERNET-CONNECTOR/actions/runs/43796188343) passed all native and Windows build, PowerShell parse, embedded SHA-256 and offline smoke tests.
- Confirmed downloadable `gui-development-build` artifact; this is **not** a GitHub Release or evidence of actual RG35XX+ hardware operation.
- [Issue #4](https://github.com/Propeex/RG35XX-PLUS-2024-KNULLI-USB-INTERNET-CONNECTOR/issues/4) remains open for physical device validation. No prerelease has been published.

## 2026-11-16 — First real Windows GUI feedback, portable ZIP revision (PR #9)

- User attached a screenshot: Google Platform-Tools 37.0.1 installed and embedded backend verified; GUI still reported **device disconnected** despite USB connection. The `Language` label was visibly cropped; the initial Google SDK prompt appeared in English.
- Product clarification: user wants **an unpacked ZIP folder with an EXE and editable support files**, *not* a standalone single-EXE distribution. Official Google Platform-Tools should be downloaded to the same folder.
- Changed distribution from embedded EXE backend to verified sidecar `backend/`, added Korean portable-install instructions, and assembled `windows-portable-zip` plus the regular CI folder artifact. Normal application versions will update EXE+backend as one verified release ZIP; `platform-tools/` and `data/` are preserved.
- New startup flow: user chooses Korean/English/Japanese before a localized Google SDK license dialog. Main-window language selector label widened and selected language saved in `data/settings.json`. Sensitive SSH keys and diagnostics remain under Windows per-user LocalAppData.
- Found code-level ADB false-negative: literal tab `\\tdevice` test was too restrictive. Updated to whitespace token parsing, added diagnostic logging and fallback from 5056 to existing shared 5037; STOP must never intentionally kill other apps' port 5037 server.
- Added a ZIP staging/verifier and rollback updater with offline CI validation. An early CI run failed its newly added portable ZIP verification test; we instrumented errors to diagnose and fix it rather than releasing.
- **Pending:** final passing CI of the whole portable artifact + user physical test of actual attached device and connect/disconnect. No hardware-ready GitHub Beta Release has been made.


## 2026-11-16 — PR #9 merged after Windows ZIP and ADB parser CI

- Final source commit `6e3b58b9d788bee6130b83a19b67a6400188f7cf` passed both jobs in Actions run `43800558352`.
- Earlier CI caught a ZIP validator limit below the actual ~162MB unpacked self-contained EXE (raised to bounded 250MB individual / 400MB total) and a C# exception filter compile mistake (corrected). The final entire workflow passed.
- PR #9 squash-merged to main: `5e2a7724a86fa7f790df8d5eb7d53803b7d8916b`.
- Verified development artifacts existed: `gui-development-build` and `windows-portable-zip`. No GitHub Release was created.
- Remaining actual-device acceptance: attached USB must display Ready, Connect/Disconnect must work, SSH process lifecycle, KNULLI IP alias, native HTTPS, ScreenScraper, theme download, PortMaster and stale-state recovery. Also test portable ZIP GitHub Release updater restart/rollback and first-run SDK installation with user-selected language.


## 2026-11-16 — START/STOP regression log analysis and follow-up branch

- Hardware logs provided by the user: START 13:00:01→13:00:07 detected the authorized ADB device and entered stale-state cleanup. KNULLI `stop-fullnet.sh` reported `STOP COMPLETE`, but the Windows script incorrectly concluded `active` or `rgusb0` remained and aborted before starting the new tunnel. A later standalone STOP at 13:07 failed at the same verification.
- **Hypothesis, pending actual device retest:** some KNULLI adbd shells may return adb exit 0 even when remote `test -e` or `ip link show` failed. This would make both previous `Exists` and `TunExists` checks return true regardless of remote state. Preserve all evidence; do not claim proof of the underlying firmware behavior.
- Branch `fix/device-state-ui-stop-verification` adds explicit remote output markers for file/TUN existence, inspects the remote STOP script exit marker, verifies `active` and `rgusb0` independently with bounded retries, and logs both outcomes. Do not delete unknown ownership markers or unowned network interfaces.
- An independent ADB native HTTPS health probe now requires a positive shell-produced marker. Merely having an `active` file, an SSH PID marker or `adb.exe` exit 0 is not Internet success. The PowerShell runner streams stage messages during operation rather than after exit.
- Product UX requirements from the same conversation: initial `Language` chooser is the only visible text before selection; KO/EN/JA localized flow starts immediately. Main window has two *separate* binary indicators: device (green connected/gray disconnected) and Internet (green connected/red disconnected), hiding Internet when no device. Activity heading is localized and visible log starts empty each GUI launch. The top-right combo displays only the current language. ADB polling improves via Windows device-change notifications, shorter cycle/timeout and rejection of multiple authorized devices.
- CI/unit smoke tests are not proof of actual START/STOP, native HTTPS, ScreenScraper or PortMaster function. Real RG35XX+ (2024) USB reconnect, stale-state, STOP and rollback tests remain release gates. No beta Release yet.


## 2026-11-17 — ADB coexistence hardware tests and beta.2 stabilization (PR #12)

- Published `v0.1.0-beta.1` on 2026-11-16; it contains the branded small launcher, framework-dependent EXE and Windows/ARM64 CI-tested backend. The user previously achieved one native USB Internet connection, but exact full START/STOP on the current ZIP remains unverified.
- First PR #12 candidate used passive ADB smart-socket queries and `host:track-devices`, but still started **port 5056** if the GUI started first. Hardware testing reproduced launch-order contention: KNULLI USB Link first and ADB Explorer second resulted in BOTH apps losing device recognition, while the reverse order worked.
- Changed new sessions to the **same shared 5037 server** as ADB Explorer. Hardware user tested both app orderings, closing/reopening the GUI, closing Explorer and device attachment after selecting/resetting KNULLI USB ADB mode. Device detection and coexistence succeeded, whether or not the background adb.exe already existed. The app intentionally leaves shared adb.exe running on exit to avoid disrupting other clients.
- **Unsolved USB re-enumeration issue:** Once detected, physically unplugging the device and reconnecting it causes BOTH KNULLI USB Link and ADB Explorer to lose detection; Windows Device Manager no longer shows `Anbernic_RG35XX_Plus` until the device USB mode is reset. This strongly implicates USB enumeration/handheld gadget behavior rather than the GUI, but a Windows driver/hardware factor has not been excluded.
- The user explicitly prohibited **firmware/OS USB Gadget patches or background recovery services** for public redistribution. Keep the limitation in public beta notes; the Windows app must not silently reset device mode or auto-START Internet. Added a conditional localized KO/EN/JA reconnect hint only after an actually detected device disappears.
- Security review identified a STOP path where a failed/unknown SSH process could leave an orphan and the PID marker might be erased. Tightened Windows SSH command-line ownership matching (already includes private `-E ssh-client.log` argument, `-R 1080`, `-p 2222`, loopback root), process-exit verification, and retention of evidence when live unknown SSH is detected.
- A legacy 5056 server is never automatically killed by beta.2, since the newer GUI cannot independently prove it owns the daemon. An old active SSH tunnel is only pinned to 5056 for explicit STOP. Standard shared 5037 is never killed on GUI close.
- Added Windows PowerShell mocked process/SSH STOP regression tests; ADB fake TCP socket tests and app version/translation self-tests remain. CI must pass all Windows + ARM64 jobs before PR merge. The first prerelease does not establish physical beta.2 START→STOP→START verification.
- Preparing `v0.1.0-beta.2` as **explicitly experimental** (user requested public beta release despite unresolved USB enumeration), with portable ZIP, SHA256 checksum and digest-verifying auto-update. Stable `v1.0.0` remains blocked on real-device acceptance.


## 2026-11-17 — Post-beta.2 reliability/security audit (Draft PR #13)

- Baseline: publicly released `v0.1.0-beta.2` from main commit `2a424b15`, with Windows .NET 10 and user-verified 5037 coexistence. Public Beta 2 was **not altered** by this audit.
- Found and addressed: Google Platform-Tools may be updated while a shared/external ADB daemon keeps SDK files open; safely defer optional SDK replacement if 5037 or legacy 5056 still listens. Never kill other programs' ADB processes to force an update.
- Found and addressed: official Google SDK metadata XML may include multiple versions; select the highest *stable Windows* revision, not the first entry, with a synthetic version-order regression test. Bound GitHub/Google metadata checks to 8 seconds for installed/offline use, without shortening real ZIP downloads.
- Found and addressed: Linux STOP cleared owned PID markers even when SIGTERM failed; preserve the PID and fail STOP when still live. CI runs real isolated Linux process tests for exit, TERM ignored, unrelated PID and stale PID. POSIX /proc disappearance races are handled.
- Found and addressed: legacy PortMaster compatibility patch persisted after STOP; new STOP attempts to restore the exact injected launcher line while preserving the backup and other edits.
- Found and addressed: Windows SSH STOP could forget PID markers when CIM/WMI failed, and SSH spawn could leave an untracked process if the PID file could not be written. Added a fallback process-existence check, exact `-E` log-path ownership, and startup failure cleanup; mocked Windows process regression tests.
- WinForms improvements: KO/EN/JA safety/exit/duplicate-instance dialogs, late callback/disposed form guards, bounded visible diagnostic log, atomic first-run language save.
- Supply chain: ARM64 helpers moved from out-of-support Go 1.23 to Go 1.27.4 (November 2026 security patches). Cross-builds explicitly target ARMv8.0 and pure-Go Linux/arm64; official Go minimum Linux kernel is below KNULLI's tested 4.9. Real device binary execution still needs validation.
- Public distribution transparency: app does NOT modify firmware/USB Gadget or install persistent USB recovery, but manual Internet START **does** create temporary network state, persist network helper scripts and optionally a root SSH public key in userdata, and back up/apply a marked PortMaster launcher compatibility line; successful STOP reverts owned temporary settings.
- Remaining acceptance: exact next candidate ZIP physical START→STOP→START, PortMaster, disconnect/replug UI, beta updater, missing .NET runtime on clean Windows, Windows file-signing. USB cable re-enumeration is still unresolved and documented.
- Detailed module-by-module source findings and QA matrix: `docs/POST_BETA2_AUDIT.md`. Keep PR #13 separate from unrelated draft PR #11 and do not publish a new beta from untested native STOP changes.
