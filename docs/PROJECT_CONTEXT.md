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
4. Visible progress bars for both update processes (including validation stages).
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
