# RG35XX+ (2024) USB Internet Connector — hardware QA

> **Development build only.** The portable backend builds and passes offline tests in GitHub Actions. It has **not yet been verified on the actual RG35XX+ (2024)**. The historical v1.4 manual setup was tested, not this new EXE.

## What changed

- **No historical v1.4/v1.6 ZIP is required.** GitHub Actions builds three ARM64 Go helpers from tracked source and downloads the official MIT-licensed xjasonlyu/tun2socks v2.7.0 ARM64 release with a pinned SHA-256 check.
- The complete, checksummed backend (Go helpers, six KNULLI shell scripts, tracked Windows PowerShell orchestrator, and third-party license) is included as a readable `backend/` folder **beside** the Windows EXE.
- At startup the EXE verifies the checksums of the **adjacent** `backend/` files and runs that verified folder. Active network helpers on the device are never silently replaced.
- The first-ever startup may display a separate setup progress bar. Later launches perform update checks before the main screen without a progress bar. **No update progress appears in the main GUI.**
- Only one central button controls **Connect / Disconnect**. All GUI action logs are English; Korean, English and Japanese are supported for buttons/status labels.
- Native DNS/HTTPS must work before the GUI displays `Connected`. The displayed status is not inferred from an old active flag or merely from ADB presence.

## Obtain a development EXE

1. Visit the [GitHub Actions build runs](../actions/workflows/build.yml). Select the newest **successful** run on PR #9's branch `fix/portable-ux-adb-detection` (or `main` after merging).
2. Download the short-lived artifact named `gui-development-build`. Extract it to a **new Windows folder**.
3. Run `RG35XXUsbInternetConnector.exe` from **the same folder as `backend/`**. No old ZIP is needed. The CI artifact `gui-development-build` is already a downloadable ZIP of these files; the separate `windows-portable-zip` artifact wraps a second inner release-format ZIP for updater testing.
4. Do **not** expect a published GitHub Release until hardware validation is complete.

## First physical-device test

0. Back up a known-working old v1.4 folder and any current device config. Do not delete/replace your working setup. End the old SSH/CMD tunnel and run its STOP script **before** testing the new EXE, if possible.
1. Windows 10/11 x64: make sure `ssh.exe` and `ssh-keygen.exe` are available from the Windows OpenSSH Client optional component. Windows internet is required for the first Google Platform-Tools download.
2. On RG35XX+ (2024): KNULLI Scarab; enable USB ADB and SSH, connect via a working USB-C data cable.
3. Start EXE. The first screen shows **only "Language" plus the language selector**. Choosing KO/EN/JA immediately enters the localized setup and license prompt. Official Google Platform-Tools download into `platform-tools/` **beside the EXE**; the adjacent `backend/` passes a checksum check.
4. Once the **device indicator is green "Connected"**, its separate Internet indicator should appear in **red "Disconnected"**. Only after the native HTTPS check succeeds may Internet turn green. Click the **single Connect Internet button**. The backend checks root ADB access, creates an app-specific SSH key, sets up ADB forward `tcp:2222` to KNULLI SSH, starts a hidden reverse SSH SOCKS tunnel and deploys TUN/DNS helpers.
5. Wait for the GUI to report **Connected** and check KNULLI system network status: `rgusb0` and IP display. Test ScreenScraper download, theme download and PortMaster startup/update/port installation.
6. Click **Disconnect Internet**. Confirm that the **device indicator remains green** (the USB cable is still attached) and the **Internet indicator turns red**, and that SSH/ADB owned by the connector stop, KNULLI `rgusb0` is removed, native route/resolver restored, and game/PortMaster files remain untouched.
7. Unplug and replug the USB cable while the GUI is open. The device indicator must promptly turn **gray "Disconnected"** on removal, **hide** the Internet indicator, and restore green/red indicators upon replug. Only after normal START/STOP passes, test unplug during connection, unplug during STOP, stale TUN state, quit-with-connection-running, and repeat connection twice.

### Re-test for the 2026-11-16 STOP regression

User logs `20261116-040001-000-start.log` and `20261116-040700-631-stop.log` show KNULLI reporting `STOP COMPLETE`, immediately followed by `KNULLI STOP verification failed: active flag or rgusb0 still exists.` The code previously relied on the `adb shell` process exit status when checking remote existence. Some adbd implementations may not propagate a remote command's failure; this is a **code-based hypothesis, not yet hardware-proven**.

The revised orchestrator reads explicit `RGUSB_PRESENT` / `RGUSB_ABSENT` markers, checks `active` and `rgusb0` separately, retries brief teardown delays, and reports any remaining resource precisely. The independent Internet health probe also requires a positive `RGUSB_NATIVE_OK` marker, not just `adb.exe` returning zero.

When retesting, preserve the full start/stop diagnostic files and look for `STOP verification 1/4: active=False; rgusb0=False`. If either remains True, collect a sanitized status report (never SSH credentials). Backend stages should appear **while the operation runs** rather than only on completion. No beta Release until actual hardware behavior is confirmed.

## Important safety limits

- **Do not** run this experimental EXE while a separate instance of the previous manual tunnel is working. The engine refuses some unrelated active SOCKS and TUN states; it does not intentionally take ownership of them.
- It uses a dedicated ADB server port `5056`; unrelated normal ADB clients generally use `5037`. No blind `taskkill /F /IM adb.exe`.
- If USB is unplugged during STOP, the Windows SSH/ADB cleanup can be attempted but KNULLI remote cleanup **cannot be confirmed**. A failed STOP is not a success.
- On restart with an existing connector SSH PID marker, EXE and Google Platform-Tools updates are **deferred** rather than overwriting an active ADB/SSH process. Disconnect before expecting updates.
- OpenSSH host-key checks use a private `known_hosts` file; the app does not bypass host-key verification.
- SSH private key is created only in the Windows user's data folder and must **never** be added to GitHub or attached to a bug report.
- USB TUN/tun2socks was proven for ordinary TCP/DNS/HTTPS on the older build. UDP/ICMP, all PortMaster games and automatic reconnection are **not guaranteed**.
- The application EXE is currently unsigned and has not undergone installer/AV compatibility QA.

## Logs and bug reports

- GUI bottom panel: localized heading and **English current-session activity events only**. This view starts fresh whenever the EXE is reopened. Full technical logs are retained separately.
- Full per-operation diagnostic files: `%LOCALAPPDATA%\RG35XXUsbInternetConnector\backend-logs`.
- Owned SSH PID/key/host records: `%LOCALAPPDATA%\RG35XX_USB_FullNet` (do **not** share private keys).
- Source-controlled portable backend: `backend/` **beside the EXE** (verify before use).
- For a failure report, record the button clicked, the English error, stage and Windows/KNULLI versions. **Redact** local user paths, ADB serial numbers, host keys and secrets from public issues.

## Release gates

GitHub Actions (Windows compiler, Windows PowerShell parser, embedded SHA-256 validator, Go tests/ARM64 binaries and shell syntax) is necessary but **not sufficient** for a Beta Release.

Before a GitHub prerelease, confirm real-device startup, connect, stop, restart, stale state cleanup, IP display, native HTTPS, ScreenScraper, themes and PortMaster. Then validate Google Platform-Tools bootstrap and at least one GitHub EXE auto-update/restart and rollback case.

See [issue #4](../issues/4), [roadmap](ROADMAP.md), [architecture](ARCHITECTURE.md) and [development log](DEVELOPMENT_LOG.md).


### Final manual regression order (after PR #10 CI)

Use a **freshly extracted** `windows-portable-zip` from the latest PR workflow; do **not** test an older Actions artifact. Back up the known-working directory first. Leave the existing portable installation untouched, and use one GUI instance at a time.

1. **First-run locale:** remove only test-folder initialization/settings markers (do not delete a production folder). Launch and confirm the only text is `Language` plus KO/EN/JA choice names. Choose Korean, then repeat clean-folder tests in English and Japanese. Every visible setup/SDK prompt and progress description must honor the choice. Technical backend messages are intentionally in English after the GUI appears.
2. **USB detection:** without USB, device label is gray/disconnected and Internet label absent. Connect KNULLI with ADB enabled; device becomes green/connected and Internet red/disconnected. Unplug and replug to verify status transitions; no automatic Internet connection should occur.
3. **First manual START:** click Connect once and observe live English checkpoints `Stage USB forwarding`, `Stage SSH/SOCKS`, `Stage device payload`, `Stage TUN/DNS`, `Stage native HTTPS`, and `Stage compatibility`. Record `USB action elapsed: ... seconds`. No green Internet badge unless an independent native HTTPS check succeeds.
4. **Manual STOP:** click Disconnect once. Look for `STOP verification 1/4: active=False; rgusb0=False` (or a later retry with both False), then `STOP completed and verified`. Device stays green; Internet turns red. Repeat START→STOP once more to detect stale resources.
5. **Normal cancellation:** if a connection attempt is in flight, unplug once and confirm the action cancels without automatic retry. Reconnect physically, allow ADB to become ready, and **manually** begin again (a stale SSH marker may need an explicit Disconnect first). Do not intentionally interrupt firmware storage writes.
6. **App session:** close/reopen the EXE. The Activity Log view starts fresh, current language remains selected, and the program should not automatically Connect. Attempt to launch a second GUI simultaneously; it should refuse without disrupting the first.
7. **Updater:** stage two trusted signed/versioned test releases in a controlled test directory to verify successful upgrade, deliberate corrupt-new-EXE rejection, and preservation of `data/` and `platform-tools/`. This cannot be proven by ZIP smoke tests alone. Do not create the public beta until the physical START/STOP passes.

Retain the `%LOCALAPPDATA%\\RG35XXUsbInternetConnector\\backend-logs` START/STOP files for developer analysis if the test fails. Never include SSH private keys, complete device serials, or unredacted sensitive paths in reports.

### SSH key authentication failure recorded on 2026-11-16

The user's 14:44 START advanced through ADB discovery and `tcp:2222` forwarding, but OpenSSH exited at 14:44:27. The private SSH client log recorded `root@127.0.0.1: Permission denied (publickey,password).` The host key was added normally; this **establishes an SSH authentication failure**, not an Internet/TUN/DNS failure. The exact underlying cause (key installation path, permissions, server options, or mismatched key files) has not yet been observed.

The new authentication checks do not overwrite or remove existing SSH credentials:
- Resolve the root user's SSH home from its passwd entry rather than assume a fixed directory. Keep safe symlink guards.
- Verify that the remote `authorized_keys` includes the requested public key, then require an explicit `RGUSB_SSH_KEY_READY` marker (the ADB exit code alone is insufficient).
- Compare the local public and private key pair before using it; abort rather than mutate mismatched credentials.
- Pass `IdentitiesOnly=yes` and force public-key-only SSH authentication; write bounded verbose SSH connection diagnostics **only to the existing private** `ssh-client.log`, resetting the previous attempt before each START.
- Surface a sanitized `SSH root public-key authentication rejected` error when the client reports authentication rejection. Avoid logging public key text, private keys, device serials or unsanitized verbose logs in the GUI.

Retest once using a new artifact from the **latest PR CI**. If still failing, review the local `%LOCALAPPDATA%\\RG35XX_USB_FullNet\\ssh-client.log` for `Offering public key` and `Server accepts key` lines and share a redacted copy. Never upload `rg35xx_usb_gui_ed25519` (private key). If `RGUSB_SSH_KEY_READY` validation itself fails, the START log should now reveal that before launching SSH.

### Default KNULLI `linux` password compatibility — PR #10

KNULLI's official SSH guide documents `root` with initial password `linux`. The existing GUI generated an SSH key and invoked Windows OpenSSH with `BatchMode=yes`, `PasswordAuthentication=no`, and `PreferredAuthentications=publickey`. When the device rejected its key, authentication stopped immediately. The hardware log reported `Permission denied (publickey,password)` before any native tunnel or DNS code ran.

The connector now attempts its private SSH key first; **when the server declines the key, it automatically answers one SSH password prompt with the KNULLI default `linux`**. It uses the published GUI EXE itself as the headless `SSH_ASKPASS` responder, with `SSH_ASKPASS_REQUIRE=force`. The password is not placed in the SSH process command line or written to a file or activity log. Password auth is restricted to the existing `127.0.0.1:2222` ADB-forwarded SSH endpoint; host-key verification is kept at `accept-new`, never globally disabled. Public-key setup errors no longer preclude the default-password fallback. CI executes a headless responder smoke test using the published WinExe with redirected standard output.

**Security/compatibility:** The program does not modify the device's root password. A device with KNULLI's randomized root password (e.g. after enabling enforced security) will not authenticate using the fixed default. Recommending all users change their root password to a publicly known value is **not safe** when SSH can be exposed to a LAN; keep strong unique passwords when possible. The preferred long-term design is per-device password input or working public-key authentication. Windows OpenSSH askpass behavior varies by system version; test on actual Windows 10/11 and review the private `ssh-client.log` if fallback fails. Do not upload passwords, private keys, or full unsanitized account information.

For the next hardware test, download the **latest** PR #10 `windows-portable-zip` artifact after CI succeeds, extract to a separate folder, and click Connect once. On success, log should pass `Stage SSH/SOCKS` and proceed to `Stage device payload`; only after native HTTPS succeeds is Internet shown green. Record the START log and private `ssh-client.log` (redacted) on failure.
