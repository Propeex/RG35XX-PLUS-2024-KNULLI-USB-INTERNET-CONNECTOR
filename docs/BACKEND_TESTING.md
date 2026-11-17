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

## First successful EXE connection and six requested improvements — follow-up

**Observed:** On 2026-11-16 the hardware owner reported their first successful RG35XX+ USB Internet connection. The same run exposed UI false-negative status, persistent Windows ADB process, intermittent USB device detection, no manual START/STOP progress, and a ~75 MiB download. This establishes **one actual connected session**, but does **not yet verify the revised status GUI, STOP, reboot, updater, or reliability.**

Changes on PR #10:
- Download size: switch the .NET 8 GUI to framework-dependent single-file and package a small `KNULLI USB Link.exe` launcher built against Windows' .NET Framework 4.x. The launcher selects language before checking for the .NET 8 Desktop Runtime; when unavailable, it asks for consent and uses Microsoft's official `dotnet-install.ps1 -Runtime windowsdesktop -Channel 8.0` for **one-time, per-user** installation. Once installed or already present, it starts the GUI. No silent global installation and no automatic Internet connection.
- Internet UI: START success already requires real device native HTTPS from the audited PowerShell backend, so display green immediately after that confirmation. An additional short independent HTTPS probe is no longer a blocking second success gate; later health checks debounce two consecutive failures.
- Dedicated ADB lifecycle: keep port 5056 alive during polling; avoid cycling it after a momentary device-miss. On normal GUI exit without an active tunnel, request shutdown **and await the operation** rather than fire-and-forget. Shared third-party port 5037 is never killed. Normal exit while Internet is active prompts to STOP first; it no longer offers an implicit leave-network-running exit path.
- Device detection: keep device status green across a single missed readiness check; use periodic ADB checks and `WM_DEVICECHANGE` hints, preserve selected server during in-flight SSH operation, and do not auto-connect.
- Manual connection progress: real **6/6 START stages** (USB forwarding, SSH, payload, TUN/DNS, native HTTPS, compatibility) and real **4/4 STOP stages** (prepare, remote, SSH, ADB). Bars represent **completed stages, not elapsed time percentages**; no progress controls for background app/Platform-Tools updates in main GUI. English detailed logs are retained, stage labels translated.
- Portable Release ZIP staging and updater now require/validate both launcher and framework-dependent EXE and back up/restore the launcher as part of the coherent update set. Public Beta remains deferred pending real release-to-release and runtime-install tests.

### Acceptance checklist for this build

1. Extract the newest successful `windows-portable-zip` to a **fresh** directory. Launch **`KNULLI USB Link.exe`**, not the internal `RG35XXUsbInternetConnector.exe`. Check the initial `Language` screen and that the selected locale controls the one-time .NET 8 Desktop Runtime prompt. If runtime already exists, it must not download it again. A real missing-runtime Windows VM is needed to validate the installer path.
2. Confirm the actual ZIP size shown in GitHub Actions. Do not confuse it with the compressed artifact wrapper or the one-time runtime download.
3. Without USB: device gray, Internet hidden. With USB and ADB: device green, Internet red until START. Temporarily missed ADB responses must not immediately reset green to gray.
4. START once. Observe stages 1–6, then **Internet green** as soon as the backend reports direct native HTTPS success; verify a second ordinary device HTTPS download still works. Record elapsed seconds. After ~40–50 seconds verify no spurious red status after periodic probes.
5. STOP once. Observe stages 1–4; verify the device stays green and Internet goes red. Try one repeat START→STOP.
6. With Internet active, close the app and choose Yes to disconnect; the app should STOP and exit. Check that the **connector-owned** ADB server on port **5056** and its owned SSH tunnel no longer run. Never assume another application's shared port 5037 must exit.
7. Reopen `KNULLI USB Link.exe`: no new language or .NET runtime download; GUI main activity log starts blank and polling adds only current-session diagnostics.
8. Hardware tests have not been run in CI: no claim of proven uninstall, new-user first-install, auto-update rollback, SSH password compatibility across all KNULLI builds, or Windows sleep behavior. Report first failing stage with the corresponding developer log; omit device serials and private keys.


## Final START 6/6 hang and new KNULLI USB Link branding

The hardware owner confirmed device Internet works but the GUI stayed on **6/6**, the Connect button never changed to Disconnect. This is distinct from initial Internet health detection. The Windows GUI previously redirected PowerShell stdout/stderr and read both to **EOF after the PowerShell process terminated**. The persistent `ssh.exe` reverse-tunnel child may inherit one of these write handles, keeping the pipe open even after its PowerShell parent exited. The UI therefore kept `_busy=true`, even though KNULLI native networking succeeded. The confirmed code defect is the unbounded pipe-EOF dependency; inheritance behavior on the user's specific device is the leading hypothesis, not yet recorded with a process trace.

The runner now streams diagnostics only through the existing private log file; it does **not redirect PowerShell output pipes**. It waits for the PowerShell process itself, not for EOF from a child that is intentionally supposed to stay alive. A maximum five-minute START/STOP timeout is still enforced. The GUI must set Internet green, hide the stage bar, and switch its button to Disconnect **after the backend process exits**. Stage 6/6 is an operation-stage indicator, not the completion state. The STOP side must continue to preserve the verified-owned-tunnel cleanup.

Branding: users now launch **`KNULLI USB Link.exe`** (small .NET Framework bootloader). Both the launcher and internal `RG35XXUsbInternetConnector.exe` embed a custom USB-C → globe icon, generated from original source art under `assets/knulli-usb-link-icon.png.base64` with `scripts/Build-AppIcon.ps1` at build time. The internal EXE name remains stable for SSH_ASKPASS and updater backward compatibility. The first-run language and .NET 8 setup remain unchanged.

Regression acceptance:
1. Start from a clean latest Actions ZIP. Double-click `KNULLI USB Link.exe`; verify the branded icon on Explorer/Windows taskbar and first-run language selector.
2. Press Connect. Confirm stage 6/6 does **not** remain visible after the backend reports `USB Internet action completed successfully.`; the Internet label goes green and the button switches to Disconnect without relaunching.
3. Wait 1 minute, then click Disconnect and verify the SSH process, KNULLI rgusb0 route, owned port 5056 and button state reset; do not kill any independently owned port 5037 ADB daemon.
4. Repeat START/STOP once. If 6/6 persists, provide the new `*-start.log` showing timestamps between `Stage compatibility`, `Connected:`, and `USB Internet action completed successfully.`. No SSH private key.


## Historical first beta.2 prototype (5056-based; superseded below)

> This archived acceptance plan describes an **earlier** candidate which was subsequently tested and found to create a USB conflict when KNULLI USB Link started before ADB Explorer. Do not use its 5056-first approach. The later **shared-5037** plan below is authoritative.

The hardware user reported these six scenarios:
1. Device already connected when the app starts: GUI does not detect until USB mode reset.
2. Resetting the handheld's USB mode makes detection work.
3. App running before plugging in the device: GUI does not detect until USB mode reset.
4. Resetting USB mode after step 3 makes detection work.
5. Once recognized, closing/reopening the GUI retains detection.
6. Closing ADB Explorer disconnects device detection; only resetting KNULLI USB mode restored it.

**Code finding:** Before this patch, each 2.5-second probe could call \`adb -P 5056 devices -l\` and then \`adb -P 5037 devices -l\`. ADB's client-side \`devices\` command implicitly launches the respective server if it is not running, so the app itself could create two USB-claiming ADB servers. The observed USB-reset dependency could ALSO reflect Windows USB driver/KNULLI adbd state; it cannot be conclusively attributed to dual servers without a process trace.

**Fix and safety rules:**
- Prefer **passive host:devices-l** smart-socket queries; never create an ADB daemon merely to probe whether an existing server sees the device
- If neither server exists, start a single app-owned server at port 5056; never start 5056 while port 5037 is already listening
- If 5037 comes online while app-owned 5056 is idle, retire only the owned 5056 so the external app can share the USB device
- Use **host:track-devices** TCP stream on the selected server for live change notifications; retain 2.5-second fallback polling, suppress serials and throttle tracker retries
- While START/STOP or an SSH session is active, pin the current port and do not kill/recreate/switch servers
- If ADB Explorer stops 5037 while idle, detect its disappearance and start one dedicated 5056 server after both ports are absent; do **not** auto-START USB Internet or reconfigure the handheld's USB mode
- Terminate only a 5056 server created by this GUI instance. Never kill an unknown 5056 listener, shared 5037 server, ADB Explorer process, or reset device-side adbd as a recovery measure

**Test once in this exact order using the newest PR development ZIP (not beta.1):**
1. Start ADB Explorer *first*, leave device plugged in, then start KNULLI USB Link. Check the log says \`existing shared\` / \`server 5037\` and device is green without USB reset
2. With ADB Explorer closed and no USB connection, open KNULLI USB Link, then connect USB. Check dedicated \`5056\` is chosen and the device turns green without resetting USB mode
3. With the app recognizing the device but Internet not started, open and close ADB Explorer. Watch whether selection switches and whether the device is detected without USB reset
4. With ADB Explorer using \`5037\`, start USB Internet in KNULLI USB Link. Close ADB Explorer *only if it is safe for the connected network*. The app should not silently move an existing SSH tunnel to another port. If it loses the server, use explicit Disconnect/reconnect; no automatic Internet reconnection
5. Check START -> STOP -> START with new server selection. The GUI should complete 6/6, turn Internet green, and switch to Disconnect, without lingering owned SSH processes
6. If detection still requires USB reset, collect only the redacted **ADB server port/status messages** immediately before the reset. Report whether \`5037\` or \`5056\` was selected and whether ADB Explorer was running. This distinguishes server loss from lower-level USB/driver issues

**Coverage:** Windows CI uses an in-process fake ADB TCP server to test framing, host device queries and state-change messages, with no actual adb.exe, no attached USB hardware and no real SSH tunnel. Physical tests are still required before merging or publishing beta.2.

## ADB Explorer launch-order regression — shared 5037 release candidate

The user tested the initial beta.2 single-server discovery attempt and reported:
- **1** (USB already connected, then KNULLI USB Link starts): still not recognized until device USB-mode reset
- **3** (app already open, then USB plugged in): still not recognized until device USB-mode reset
- **6** (close ADB Explorer after detection): **fixed** in the previous candidate
- **NEW** (start KNULLI USB Link before ADB Explorer): when Explorer subsequently opens, **both apps lose the USB device** until USB-mode reset
- **Opposite order** (ADB Explorer first, then KNULLI USB Link): both work

These observations strongly implicate ADB's **5056-vs-5037 ownership conflict**. The previous patch avoided creating simultaneous servers from a single probe, but still *created 5056 first* when Explorer was not running. Explorer then started its standard 5037 daemon, apparently invalidating USB transport ownership.

**Revised design:**
- All NEW manual connections and device discovery use standard **5037**, regardless of app launch order. If no ADB server exists, KNULLI USB Link starts one at 5037 (using official Google platform-tools); Explorer subsequently shares it.
- Passive host:devices-l / host:track-devices over the selected smart-socket remains; no extra adb.exe process for idle polling.
- A stale, still-running **legacy 5056** server is *not killed or competed with*: no new 5037 daemon starts until it exits. The log says \`Legacy ADB server 5056 is still running\`. Close the previous connector process normally rather than killing an unknown adb.exe.
- Only if an existing SSH session was forwarded through legacy 5056 does it remain temporarily pinned to 5056 for an explicit STOP; **no automatic connection restart**, no USB-mode reset and no forced shutdown of 5037.
- Since 5037 is now shared, the GUI never stops 5037 on normal exit. That process may remain running to serve ADB Explorer; this is **intentional coexistence**, not an orphaned per-app daemon.
- If ADB Explorer closes and kills the shared server while Internet is connected, the established forward may be lost; the UI must not invent a restored connection. STOP and manually reconnect after it returns. Hardware confirmation needed.

**Required real-device acceptance (use the latest PR #12 \`windows-portable-zip\`, not the published beta.1):**
1. Close all previous KNULLI USB Link windows. With ADB Explorer closed, plug in RG35XX+ and start new KNULLI USB Link. Expect \`ADB device ready (server 5037, shared with ADB Explorer)\` without device USB reset
2. Close the app, leave USB plugged, open new KNULLI USB Link again. Expect immediate device recognition
3. Unplug USB, leave app running, reconnect cable. Expect recognition without device USB reset
4. **New failure case:** with app already running and recognizing device, start ADB Explorer. Expect *both* apps to keep seeing the device
5. Opposite order: start Explorer first, then KNULLI USB Link. Expect both to see the device
6. Close Explorer while app remains open (Internet disconnected). Expect app to retain or regain device recognition without USB-mode reset
7. Manually START Internet, verify 6/6 completes and button becomes Disconnect. STOP and verify remote cleanup, then START again. Do not auto-reconnect on a disconnected ADB server
8. If a case fails, copy only the lines around \`ADB server 5037\`, \`Legacy ADB server 5056\`, \`ADB device ready\`, and the relevant START/STOP stage. Avoid revealing device serials or private keys

**Hardware status:** Not yet tested. PR #12 remains Draft, no change to main or the public \`v0.1.0-beta.1\` Release.
