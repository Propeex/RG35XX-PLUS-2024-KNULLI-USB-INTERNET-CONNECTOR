# KNULLI Linux ARM64 network helpers

These **tracked, auditable sources** originated from the v1.4 connectivity
prototype and are now built by GitHub Actions for the experimental public
KNULLI USB Link Windows beta.

The native Go 1.27.4 build (supported and security-patched as of
2026-11-17) compiles three statically linked Linux/arm64 programs with
`CGO_ENABLED=0` and `GOARM64=v8.0`:

- `daemon-launch`: detaches the network helpers from the ADB shell session
- `tunctl`: creates and deletes an application-owned transient TUN interface
- `dns-bridge`: local DNS-over-HTTPS relay over the manually started SSH SOCKS tunnel

The separately maintained `xjasonlyu/tun2socks` v2.7.0 ARM64 binary is
retrieved in CI only from its pinned GitHub release asset, SHA-256-verified,
and shipped with its MIT attribution.

## Distribution and deployment

The Windows CI creates a checksummed **sidecar `backend/` directory**
containing the ARM64 binaries, device shell scripts, Windows PowerShell
orchestrator, `SHA256SUMS` and required third-party attribution. The
portable Windows ZIP includes that directory beside the EXE; the EXE checks
its hashes before manual START/STOP. No unsupported historical ZIP is needed.

Manual **START** may copy these tools into
`/userdata/system/rgusb-fullnet` and temporarily configure the owned
TUN, DNS resolver and route. It also registers the user's dedicated SSH
*public* key in root's `authorized_keys` when supported. The optional
PortMaster compatibility shim backs up the existing launcher and inserts a
marked line only when needed; STOP now attempts to remove that line while
preserving the backup and unrelated changes.

Manual **STOP** verifies removal of the application-owned networking state,
preserves PID markers for helpers that refuse to stop, and reports incomplete
cleanup instead of falsely claiming success. Scripts and the SSH public key
may remain under `/userdata` for reuse; this is **not a read-only operation**
on device user data.

The program does **not** patch KNULLI firmware, alter USB Gadget modes,
change startup services or install a background USB-recovery daemon. The
observed USB cable unplug/replug re-enumeration bug remains a known
limitation, not a feature silently repaired by these helpers.

## Verification

Both ARM64 CI workflows run `go test ./...`, POSIX shell syntax checks
and the isolated `scripts/Test-DeviceStop.sh` process ownership tests.
The Windows CI also compiles the GUI and validates the portable backend.

CI is **not** a substitute for real RG35XX+ 2024 hardware testing.
In particular the **exact new helper build** must still be checked on
KNULLI's Linux 4.9 ARM64 kernel with native Internet START → STOP → START,
DNS, PortMaster and abnormal unplug scenarios.
