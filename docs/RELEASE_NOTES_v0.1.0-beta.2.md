# KNULLI USB Link v0.1.0-beta.2

**Public beta / prerelease — 2026-11-17. Not a stable or fully hardware-verified release.**

This beta focuses on reliable Windows ADB coexistence and conservative USB/SSH state handling. It **does not patch, install services on, or otherwise attempt to repair KNULLI's USB Gadget/operating system**. Internet access is connected and disconnected **only when the user presses the button**.

## Improvements

- **One shared ADB server (port 5037):** use the same official Google Android SDK ADB server as ADB Explorer, independent of application launch order. No new competing 5056 daemon is started.
- **Passive USB monitoring:** query existing ADB servers through the local smart-socket `host:devices-l` and subscribe to `host:track-devices`; existing Windows USB notifications and periodic checks remain as safeguards.
- **External app safety:** closing KNULLI USB Link does not kill port 5037 or other programs' ADB sessions. A pre-existing active tunnel on legacy 5056 remains pinned until the user explicitly stops it. Unknown 5056 processes are never forcibly terminated.
- **Safer SSH STOP:** verify the SSH command line and exact owned loopback reverse-tunnel arguments before stopping an SSH process; wait for verified exit before clearing the ownership marker. If an unrelated live SSH PID is found, refuse to kill or forget it. An unsuccessful STOP never claims verified remote cleanup.
- **USB disconnection clarity:** after a device that was previously detected disappears, show a small, localized Korean/English/Japanese hint explaining that KNULLI USB mode may need to be reset if Windows does not recognize the replugged device. No automatic USB mode changes or Internet reconnection.
- **Real-stage progress and connection proof:** preserve the 6-stage START / 4-stage STOP display, actual native HTTPS verification, dedicated SSH key with optional default-password fallback and compact branded launcher.
- **Regression checks:** offline fake ADB server tests and mocked Windows process-ownership tests, ARM64 helper build, PowerShell syntax check, EXE self-tests and full ZIP updater validation.

## Hardware results and known limitation

A tester confirmed that **after setting/resetting KNULLI's USB ADB mode**, KNULLI USB Link detects the device whether the app starts before or after the USB cable is connected, with or without an existing background `adb.exe`. ADB Explorer can then open and close without breaking recognition.

**Known limitation — USB cable unplug/replug:** On the tested RG35XX+ (2024, no Wi-Fi), removing the cable and plugging it in again can leave the handheld **absent even from Windows Device Manager**, so neither KNULLI USB Link nor ADB Explorer can see it. The user has to reset/reselect the handheld's **USB ADB mode** to re-enumerate it. Windows software cannot deliver ADB commands to a device that Windows has not enumerated. Do not assume this is a verified OS-only defect; Windows drivers, firmware and hardware may also contribute. This beta neither changes the OS nor claims to fix USB Gadget re-enumeration.

**Not yet independently validated on this exact beta.2 ZIP:** complete hardware Internet START 6/6 → STOP → START repetition, SSH stop ownership checks on the real Windows process, automatic release-to-release update, fresh Windows installation, suspend/resume, and all compatible Windows/firmware combinations. Passing CI is not a substitute for physical verification.

## Install

1. Download `RG35XX-USB-Internet-Connector.zip` and `SHA256SUMS.txt` below; optionally verify the ZIP SHA-256.
2. Extract the **entire ZIP** to a writable folder. Start **`KNULLI USB Link.exe`**, not the internal `RG35XXUsbInternetConnector.exe`.
3. Choose Korean, English or Japanese. Allow optional **Microsoft .NET 8 Desktop Runtime** setup if prompted, then review/accept the official Google Android SDK Platform-Tools license for first use.
4. On the **RG35XX+ 2024 Wi-Fi-less model** running KNULLI, enable USB ADB mode and SSH. Connect a data-capable USB-C cable.
5. Wait until the device is detected. Press **Connect Internet** manually. Verify the native Internet indicator, and press **Disconnect Internet** to stop it before unplugging.
6. If Windows does not see the device after unplug/replug, use the **device's existing USB mode control** to reselect/reset USB ADB mode; no firmware installation or OS modification is required.

Windows 10/11 **x64** is targeted. The ZIP does not include the Microsoft runtime, which may be installed separately with user permission. Google Platform-Tools is downloaded from its official source and stored in the program folder.

## Security and troubleshooting

- Unsigned prerelease: Windows may show a SmartScreen warning. Obtain the ZIP only from this GitHub Release and compare `SHA256SUMS.txt`.
- The firmware's `linux` root password is publicly known. The app can use that default password as an SSH fallback; do **not** change a customized secure password back to the default merely for this program.
- Never share SSH private keys, raw ADB device serials, personal paths, or unredacted private logs.
- Do not kill unknown `adb.exe` processes to force USB recognition. The shared port 5037 is intentionally left running even after KNULLI USB Link closes.
- This software is intended for the RG35XX+ (2024) model **without built-in Wi-Fi**; similarly named or other H700 models are not confirmed supported.

See the [real-hardware validation plan](https://github.com/Propeex/RG35XX-PLUS-2024-KNULLI-USB-INTERNET-CONNECTOR/blob/main/docs/BACKEND_TESTING.md) and [Korean installation guide](https://github.com/Propeex/RG35XX-PLUS-2024-KNULLI-USB-INTERNET-CONNECTOR/blob/main/docs/PORTABLE_INSTALL_KR.md).
