# Post-beta.2 full-stack audit — KNULLI USB Link

Source baseline: published `v0.1.0-beta.2`, `main` commit
`2a424b15855d860dc0bc07deeb7a8d0aeee6e0e6` (2026-11-17).
Audit fixes: **PR #13**, branch `fix/post-beta2-stability-audit`.

**Release policy:** This branch is NOT a public release. Its source/CI can be
reviewed, but it should not be merged or published as a stable build before
the real RG35XX+ 2024 START → STOP → START hardware regression is run.
The published beta.2 ZIP remains unchanged.

## Scope and results

| Area | Baseline behavior | Finding / audit outcome | Risk |
| --- | --- | --- | --- |
| Launch/runtime | Compact .NET Framework 4.x launcher starts .NET 10 LTS WinForms GUI | Language settings were deleted before a new file was moved; changed to atomic replacement. Missing-runtime path still requires a Windows installation test | Medium |
| First-run / locale | KO/EN/JA selector, SDK consent, no automatic Internet START | English-only active-connection exit prompts and duplicate-process notification localized; technical activity logs stay English | Low |
| GitHub release update | GitHub API + digest, staged ZIP self-tests, copy-based updater with rollback | Release metadata could hang for the five-minute shared HTTP timeout; limited *metadata* request to 8s. Actual ZIP download retains the longer deadline | Medium |
| Platform-Tools | Official Google Android SDK, verified hash when repository supplies one, folder staging and backup | Old logic selected the first Windows SDK archive instead of the highest stable revision; fixed with XML fixture test. SDK update is deferred if 5037/5056 ADB daemon is present | High |
| ADB device detection | One shared server 5037; passive host:devices-l + host:track-devices; 5056 only for legacy pinned sessions | Preserved intact following real user confirmation that ADB Explorer start/close orders now work. No global kill-server, USB reset or Internet auto-reconnect | Verified baseline, hardware limits |
| Windows GUI lifecycle | Separate Device and Internet status; manual 6-stage START / 4-stage STOP | Late background log callback after disposal could fault; guarded. GUI log text is capped for multi-day sessions; full action logs remain on disk | Medium |
| Windows SSH/ADB | Root loopback tunnel uses dedicated public key then firmware-default password fallback if needed | Refuse to kill SSH unless exact owned `-E` log path and forward args match; retain live SSH markers when CIM fails; terminate a just-spawned SSH process when its PID cannot be recorded. Never kill shared 5037 | High |
| KNULLI STOP | Stop owned TUN, resolver, route, helper processes and Windows SSH | Original device helper STOP forgot PIDs even after a failed SIGTERM. Fixed with bounded exit verification and retained markers for live helpers, covered by disposable Linux process tests | High |
| KNULLI compatibility | Optional owned IP alias and backed-up PortMaster launcher proxy override | STOP now attempts to remove only the exact connector-added PortMaster line, retaining other user changes and original backup | Medium |
| KNULLI DNS/TUN binaries | Source-tracked ARM64 Go programs, hash-pinned MIT tun2socks v2.7.0 | Upgraded build from unsupported Go 1.23 to security-patched Go 1.27.4; explicit GOARM64=v8.0 and CGO_ENABLED=0. Kernel 4.9 passes Go's documented baseline, real execution still needs device QA | High |
| Package / supply chain | Portable ZIP, backend SHA256SUMS, Release digest, no third-party installers | CI exercises package verification, launcher/EXE self-tests, Go unit tests. The prerelease is **unsigned**, and the Microsoft runtime bootstrap and Google SDK require network trust and user consent | Residual |
| Privacy / network | No cloud telemetry from the Windows GUI; private SSH files under LocalAppData | Device DNS helper uses Cloudflare DoH, and connectivity proof queries example.com; document this external traffic honestly. Do not publish private SSH keys, raw device serial or unredacted action logs | Residual |

## Device changes and the public-distribution boundary

This app **does not patch KNULLI firmware, USB Gadget configuration, kernel
startup services or install a background USB reconnection daemon**. It cannot
repair a USB device Windows fails to enumerate after a cable replug.

Manual START is not read-only: the verified engine copies binaries/scripts
under `/userdata/system/rgusb-fullnet`, registers the connector's *public*
SSH key in root's `authorized_keys` when possible, temporarily installs
`rgusb0`, route and resolver settings, and can back up/adjust
`/userdata/roms/ports/PortMaster.sh`. Successful STOP must revert owned
temporary network settings and the exact PortMaster compatibility line,
but scripts, SSH public key and backups can remain in user data for reuse.
An interrupted or failed STOP must be reported as unverified, never as clean.

## Physical findings already confirmed

The tester's initial ADB connection became reliable after selecting/resetting
KNULLI's USB mode. The GUI and ADB Explorer can then start/close in either
order using port 5037. Background `adb.exe` continuing after GUI exit is
intentional and shared; no unrelated ADB daemon is terminated.

**Known unresolved issue:** After physical USB unplug/replug, Windows Device
Manager sometimes does not enumerate `Anbernic_RG35XX_Plus`; both ADB Explorer
and KNULLI USB Link then cannot see the handheld until its USB mode is reset.
This is a USB enumeration/device/driver limitation, **not** proven to be a
specific firmware-only bug. No OS patch is offered or required.

## Tests required before publishing these audit fixes

1. Run both Linux and Windows/ARM64 GitHub Actions jobs for the **final PR #13
   head**, not merely earlier commits. The Linux jobs execute real disposable
   helper process tests: normal exit, ignored SIGTERM, unrelated PID, stale PID.
2. Run the offline Windows fake ADB smart-socket snapshots and tracking tests,
   SDK metadata-selection fixture, SSH CIM-failure/PID reuse test, WinForms
   prerelease self-test and portable ZIP verifier.
3. Using the actual RG35XX+ 2024 and release-like ZIP: USB mode activation,
   initial attachment both orders, ADB Explorer both orders, manual START 6/6,
   native HTTPS, explicit STOP 4/4, second START, PortMaster operation and
   confirmation the helper PIDs actually disappear.
4. Unplug during idle and active session. The GUI must not auto-connect,
   claim verified STOP when remote cleanup was impossible, kill shared ADB,
   or modify USB Gadget settings. USB-mode reset remains manual.
5. On a Windows PC with only .NET 10 installed / no .NET 10 runtime, validate
   first-run launcher, license prompts, offline startup and automatic update
   fallback. Beta.1 -> beta.2/3 runtime transition may require a manual ZIP.

**Until hardware checks 3–5 are done, keep the code in PR #13 and the public
release explicitly beta.** Passing CI establishes package integrity and
offline behavior only, not physical device stability.
