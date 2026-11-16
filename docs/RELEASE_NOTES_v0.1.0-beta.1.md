# KNULLI USB Link v0.1.0-beta.1

**Beta / Prerelease — 2026-11-16.** First public testing build, **not** a stable release.

## What works so far

The RG35XX+ (2024 edition without Wi-Fi) hardware tester has confirmed **one successful KNULLI USB Internet connection** using the development build with automatic fallback to KNULLI's default SSH password. This demonstrates the USB networking path on that device; it does **not** establish that all Windows systems, later builds, START/STOP cycles, or sleep/resume cases work reliably.

## Improvements in this beta

- New branded **`KNULLI USB Link.exe`** launcher and original USB-C / network icon.
- Compact portable ZIP (~8 MB, instead of the earlier ~75 MB). On first run, the launcher prompts in Korean, English or Japanese to install **Microsoft .NET 8 Desktop Runtime** only if absent; the per-user runtime installation is not included in ZIP size.
- One manual Connect / Disconnect button and English activity log, plus localized six-stage START and four-stage STOP progress.
- SSH connectivity via dedicated key with automatic default-`linux` password fallback to a verified loopback, ADB-forwarded KNULLI SSH endpoint.
- ADB device detection debouncing, separate device/Internet states and independent native HTTPS verification.
- Stability fix for a GUI that previously remained at START 6/6 after device Internet was already working: the app no longer waits for potentially inherited SSH standard-output pipes to close.
- Conservative remote STOP verification and connector-owned ADB/SSH cleanup; other applications' shared ADB port 5037 is preserved.

## Installation

1. Download `RG35XX-USB-Internet-Connector.zip` from this Release and unzip the **entire** folder into a writable location.
2. Run **`KNULLI USB Link.exe`**, not the internal `RG35XXUsbInternetConnector.exe`.
3. Choose a language. If prompted, allow the one-time Microsoft .NET 8 Desktop Runtime setup.
4. Review the Google Android SDK Platform-Tools license and consent to its separate first-run download.
5. On your KNULLI RG35XX+ (2024), enable USB ADB and SSH, plug it into Windows by a data-capable USB cable, then press **Connect** manually.
6. Wait until the progress indicator disappears and Internet shows green. Press **Disconnect** to stop the tunnel safely before unplugging or closing.

Windows 10/11 **x64** is the intended platform. No automatic USB Internet reconnection is attempted.

## Known limitations and security

- The exact first-release ZIP's GUI START 6/6 completion, STOP, device tracking, updater and fresh-Windows .NET installation **still require community hardware validation**. If the app stays on 6/6, report the private START log's last stage; do not repeatedly force-close or kill unrelated ADB instances.
- KNULLI's `linux` root password is a publicly known **default**. The app never changes the user's root password and you should **not weaken a custom strong password** to `linux` just for this tool. Customized SSH credentials may not work yet.
- The binaries are unsigned, so Windows SmartScreen may warn. Do not bypass a warning if the source or checksum is unexpected.
- Never share SSH private keys, raw device serial numbers, or sensitive paths in issue reports.
- This beta is not a promise of compatibility with the RG35XX Plus Wi-Fi model, other H700 handhelds, or all KNULLI versions.

## Integrity

A `SHA256SUMS.txt` release asset accompanies the portable ZIP. The application's own backend is also checked with its packaged `SHA256SUMS`. GitHub Release asset SHA-256 digests are used to validate future automatic updates.

Use the [hardware testing guide](https://github.com/Propeex/RG35XX-PLUS-2024-KNULLI-USB-INTERNET-CONNECTOR/blob/main/docs/BACKEND_TESTING.md) for regression steps.
