# Developer test: temporary USB engine bridge

> **Not a production installer or a tested beta.** See issue #4. The old v1.6 network scripts were not fully validated on the user's physical device; only the historical v1.4 TUN/DNS network was verified.

## Why the temporary ZIP requirement exists

This repo now contains the source for the three native Linux ARM64 Go helpers and the KNULLI shell scripts. However, the full Windows v1.6 PowerShell orchestration is being audited/migrated separately, and the new EXE's native build packaging has not been finished. We deliberately **do not redistribute an opaque legacy ZIP** in a public EXE.

For local device testing, the GUI can import exactly one historical archive by a pinned SHA-256:

- Filename: `RG35XX_2024_USB_Full_Internet_v1_6_GUI.zip`
- SHA-256: `3f69756af1825eb6738dca25e0c9222dc43d17991fbc051670fce7f5778d015d`
- This ZIP originally came from the earlier ChatGPT conversation; there is **no public download URL** for it in this repo. Do not accept similarly named unverified ZIPs.

## How to test the current development EXE

1. Visit GitHub Actions for this branch and choose the latest **successful** `Windows EXE build (development)` run. Obtain its short-lived `gui-development-build` artifact and extract it. This is **not** a GitHub Release.
2. Copy your exact historical `RG35XX_2024_USB_Full_Internet_v1_6_GUI.zip` next to `RG35XXUsbInternetConnector.exe` (not into Platform-Tools).
3. In PowerShell, verify the old archive before running:
   `(Get-FileHash .\RG35XX_2024_USB_Full_Internet_v1_6_GUI.zip -Algorithm SHA256).Hash`
   It must equal the SHA-256 above.
4. On the handheld, ensure KNULLI USB MODE=ADB, SSH=ON. Connect by USB-C to Windows 10/11. The Windows OpenSSH Client (`ssh.exe` and `ssh-keygen.exe`) must be installed.
5. Launch the EXE. On first setup, accept the Google Android SDK terms if you agree. Platform-Tools are downloaded from Google into `%LOCALAPPDATA%\RG35XXUsbInternetConnector\platform-tools`.
6. The startup importer verifies the old ZIP SHA and extracts it into the application-owned user-data directory. It updates **only** the legacy script's local ADB path so that the EXE uses the newly installed official Platform-Tools.
7. Once the device is ready, click **Connect Internet**. This launches a **hidden PowerShell** process, which starts the existing SSH/TUN/DNS scripts and tests native HTTPS.
8. If it reports Connected, check KNULLI IP display, ScreenScraper, theme downloads and PortMaster.
9. Click **Disconnect Internet** and verify KNULLI returns to its prior network state. Test unplug/reconnect only after the initial start/stop test works.

## Troubleshooting / reporting

- English GUI activity log contains high-level operation and failure messages.
- Full legacy backend diagnostic logs (historically Korean+English) are retained under `%LOCALAPPDATA%\RG35XXUsbInternetConnector\backend-logs`. They are **not** shown directly inside the English-only GUI log.
- Windows SSH key and owned PID information are stored under `%LOCALAPPDATA%\RG35XX_USB_FullNet`; never upload any SSH private key or known_hosts file. Scrub usernames and ADB serials from logs before filing a public issue.
- Imported engine is left untouched while running. If its import directory is incomplete, stop any legacy sessions before correcting it; the app refuses to replace live executable files automatically.
- The current imported v1.6 backend uses an isolated ADB server port `5056`; GUI status polling preserves it while its SSH tunnel may still be active. An explicit STOP shuts down that app-specific server (not other apps' default 5037).
- If the device disconnects during STOP, the old backend still tries to stop Windows-owned SSH and ADB; KNULLI-side cleanup cannot be verified until the device reconnects.
- Do **not** run `taskkill /F /IM adb.exe` without understanding it kills unrelated debugging sessions.

## Outstanding acceptance criteria

- Validate a fresh Windows first-run download, update and restart flow (issues #2/#3).
- Migrate v1.6 PowerShell orchestration to tracked source, audit and eliminate legacy ZIP import.
- Integrate the reproducibly built ARM64 helper binaries from native CI into a signed/verified EXE installation procedure.
- Test start/stop/recovery on actual RG35XX+ (2024), including stale active state, USB cable unplug, restart, and PortMaster.
- Release beta only after user reports successful hardware testing.
