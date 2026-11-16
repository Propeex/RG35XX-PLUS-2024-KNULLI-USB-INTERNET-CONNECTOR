# Native device helpers (prototype sources)

These files were migrated from the user's prior **v1.4** KNULLI USB connectivity prototype, instead of copying opaque ELF executables into this public repository.

- `daemon-launch`: starts helpers in separate sessions so they survive ADB shell termination.
- `tunctl`: creates/deletes a temporary persistent Linux ARM64 TUN device.
- `dns-bridge`: localhost-bound DoH relay through the reverse SSH SOCKS tunnel.

They use only Go's standard library. The native CI workflow tests the DNS code and builds reproducible **Linux ARM64** executables. A compiled binary is **not** automatically released or installed on users' machines yet; license/ownership must be reviewed before distribution. 

`tun2socks` is a separate third-party project by Jason Lyu (`xjasonlyu/tun2socks`), licensed MIT as of v2.7.0. The historical backend downloads that precise release from GitHub with its pinned SHA-256. We do not bundle tun2socks in this source repository. Its license and attribution must be included in any future official binary distribution.

## Device shell scripts

Original KNULLI `start-fullnet.sh`, `stop-fullnet.sh`, `compat-fullnet.sh`, `check-fullnet.sh`, `watch-fullnet.sh` and `setup-ssh-key.sh` have been migrated into `native/device/`. The Linux CI workflow validates their POSIX shell syntax. This is source audit/migration, **not** evidence of automatic deployment or physical-device reliability.

## CI embedding pipeline

The primary GitHub Actions Windows build workflow now compiles the three Go ARM64 helpers, verifies the exact upstream xjasonlyu/tun2socks v2.7.0 ARM64 ZIP SHA-256, includes `third_party/tun2socks-LICENSE.txt`, bundles the tracked PowerShell + shell sources with `SHA256SUMS`, and embeds them in the WinForms EXE. No historical local ZIP is required for that build. `native.yml` continues to test and independently build native ARM64 helpers. This is not evidence of real-device runtime success.
