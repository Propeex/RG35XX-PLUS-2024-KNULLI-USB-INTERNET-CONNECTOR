# Tracked Windows orchestration for RG35XX+ (2024) / KNULLI USB Internet.
# Requires Windows PowerShell 5.1, Google adb.exe and Windows OpenSSH Client.
# Binary helpers and device scripts come from the EXE's SHA-256 verified payload.
# No EmulationStation restart, no Wi-Fi module modifications, no hidden firmware edits.
param(
    [ValidateSet('start','stop','status')][string]$Action='status',
    [string]$LogFile=''
)
$ErrorActionPreference = 'Continue'
$script:root = $PSScriptRoot
$script:adb = $env:RG35XX_ADB_EXE
if ([string]::IsNullOrWhiteSpace($script:adb) -or -not (Test-Path -LiteralPath $script:adb)) {
    throw 'Official Google adb.exe was not supplied by the application.'
}
$script:port = if ($env:ADB_SERVER_PORT -match '^[0-9]{4,5}$') { $env:ADB_SERVER_PORT } else { '5037' }
$script:base = '/userdata/system/rgusb-fullnet'
$script:state = Join-Path $env:LOCALAPPDATA 'RG35XX_USB_FullNet'
New-Item -ItemType Directory -Force -Path $script:state | Out-Null
if ([string]::IsNullOrWhiteSpace($LogFile)) {
    $LogFile = Join-Path $script:state 'last-action.log'
}
$script:LogFile = $LogFile
[IO.File]::WriteAllText($LogFile,'',(New-Object Text.UTF8Encoding($false)))
$script:serial = $null
$script:sshStartedHere = $false
$script:connectionHealthy = $false

function Log([string]$Message) {
    $line = ('{0}  {1}' -f (Get-Date -Format 'HH:mm:ss'),$Message)
    try {
        [IO.File]::AppendAllText($script:LogFile,($line+[Environment]::NewLine),
            (New-Object Text.UTF8Encoding($false)))
    } catch { }
    if ($env:RG35XX_GUI -ne '1') { Write-Host $line }
}
function Fail([string]$Message) { throw $Message }
function Need([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) { Fail ("Missing required file: " + $Path) }
}
function RunAdb([string[]]$Argv) {
    $prefix = @('-P',$script:port)
    if ($script:serial) { $prefix += @('-s',$script:serial) }
    if ($Argv.Count -gt 0 -and $Argv[0] -eq 'push') {
        & $script:adb @prefix @Argv 1>$null 2>$null
        return [pscustomobject]@{Text='';Code=[int]$LASTEXITCODE}
    }
    $raw = & $script:adb @prefix @Argv 2>&1
    return [pscustomobject]@{
        Text=($raw | Out-String).Trim()
        Code=[int]$LASTEXITCODE
    }
}
function MustAdb([string[]]$Argv,[string]$Purpose) {
    $result = RunAdb $Argv
    if ($result.Code -ne 0) {
        Fail ("ADB {0} failed (exit {1}): {2}" -f $Purpose,$result.Code,$result.Text)
    }
    if ($result.Text -and $Argv[0] -ne 'shell') { Log $result.Text }
    return $result.Text
}
function Discover() {
    Log 'Discovering exactly one ready ADB device.'
    $lines = RunAdb @('devices','-l')
    if ($lines.Code -ne 0) { Fail "adb devices failed: $($lines.Text)" }
    $devices = @()
    foreach ($line in ($lines.Text -split '[\r\n]+')) {
        if ($line -match '^([^\s]+)\s+device(?:\s|$)') { $devices += $Matches[1] }
    }
    if ($devices.Count -ne 1) {
        Fail ("Expected one authorized ADB device; found {0}. Enable KNULLI USB ADB mode." -f $devices.Count)
    }
    $script:serial = $devices[0]
    Log 'ADB device found. Serial suppressed in application log.'
    $who = RunAdb @('shell','id -u')
    if ($who.Code -ne 0 -or $who.Text.Trim() -ne '0') {
        Fail 'KNULLI ADB shell must have root privileges.'
    }
}
# Some KNULLI adbd versions do not forward the remote shell's exit status.
# Explicit result markers avoid false positives from "adb shell test ..." returning 0.
function RemoteCheck([string]$Condition) {
    $r = RunAdb @('shell',"if $Condition; then echo RGUSB_PRESENT; else echo RGUSB_ABSENT; fi")
    if ($r.Code -ne 0) { Fail ("ADB remote state probe failed (exit {0})." -f $r.Code) }
    $answer = $r.Text.Trim()
    if ($answer -eq 'RGUSB_PRESENT') { return $true }
    if ($answer -eq 'RGUSB_ABSENT') { return $false }
    Fail 'ADB remote state probe produced an unexpected response; refusing to guess.'
}
function Exists([string]$Remote) {
    return (RemoteCheck "test -e '$Remote'")
}
function TunExists() {
    return (RemoteCheck 'ip link show rgusb0 >/dev/null 2>&1')
}
function CheckNative() {
    $r = RunAdb @('shell',"curl --noproxy '*' --max-time 15 -sSI https://example.com")
    return ($r.Code -eq 0 -and $r.Text -match '(?m)^HTTP/(?:1\.[01]|2|3)\s+[23][0-9][0-9]')
}
function CheckSocks() {
    $r = RunAdb @('shell','curl --connect-timeout 2 --max-time 4 -sSI --socks5-hostname 127.0.0.1:1080 https://example.com')
    return ($r.Code -eq 0 -and $r.Text -match '(?m)^HTTP/(?:1\.[01]|2|3)\s+[23][0-9][0-9]')
}
function Push([string]$Local,[string]$Remote) {
    Need $Local
    $r = RunAdb @('push',$Local,$Remote)
    if ($r.Code -ne 0) { Fail ("Cannot upload {0} (exit {1})" -f $Local,$r.Code) }
}
function PrepareStop() {
    if (-not (Exists $script:base)) { return }
    Push (Join-Path $script:root 'device/stop-fullnet.sh') "$script:base/stop-fullnet.sh"
    [void](MustAdb @('shell',"chmod 755 $script:base/stop-fullnet.sh") 'chmod STOP')
}
function StopDevice() {
    Log 'Stopping KNULLI TUN, DNS helper and temporary route.'
    if (Exists "$script:base/stop-fullnet.sh") {
        # Make the remote exit code observable even when adbd returns success for
        # an unsuccessful shell command.
        $command = 'sh {0}/stop-fullnet.sh; exitcode=$?; echo RGUSB_STOP_EXIT:$exitcode' -f $script:base
        $r = RunAdb @('shell',$command)
        $output = ($r.Text -replace '(?m)^RGUSB_STOP_EXIT:[0-9]+\s*$','').Trim()
        if ($output) { Log $output }
        if ($r.Code -ne 0 -or $r.Text -notmatch '(?m)^RGUSB_STOP_EXIT:0\s*$') {
            Fail 'KNULLI STOP reported incomplete cleanup or could not return its exit status.'
        }
    } elseif ((Exists "$script:base/active") -or (TunExists)) {
        Fail 'Active network artifacts exist, but the owned STOP script is missing.'
    }
    # Check each independently, and permit a brief interface teardown delay.
    for ($attempt = 1; $attempt -le 4; $attempt++) {
        $activePresent = Exists "$script:base/active"
        $tunPresent = TunExists
        Log ("STOP verification {0}/4: active={1}; rgusb0={2}." -f
            $attempt,$activePresent,$tunPresent)
        if (-not $activePresent -and -not $tunPresent) {
            Log 'KNULLI STOP verified: owned active flag and rgusb0 removed.'
            return
        }
        if ($attempt -lt 4) { Start-Sleep -Milliseconds 500 }
    }
    Fail ("KNULLI STOP verification failed: active={0}; rgusb0={1}." -f
        $activePresent,$tunPresent)
}
function ProcessForOwnedSSH() {
    $pidfile = Join-Path $script:state 'ssh.pid'
    if (-not (Test-Path -LiteralPath $pidfile)) { return $null }
    $text = (Get-Content -LiteralPath $pidfile -Raw).Trim()
    if ($text -notmatch '^[0-9]+$') { return $null }
    $p = Get-CimInstance Win32_Process -Filter "ProcessId=$text" -ErrorAction SilentlyContinue
    if (-not $p -or $p.Name -ne 'ssh.exe') { return $null }
    # Public-key setup can fail while the user successfully authenticates
    # with the KNULLI password. Such a session has no -i key argument.
    # Require our private diagnostics path and the exact loopback tunnel
    # instead of the optional identity filename.
    if ($p.CommandLine -notmatch 'ssh-client\.log' -or
        $p.CommandLine -notmatch 'root@127\.0\.0\.1' -or
        $p.CommandLine -notmatch '(\s|^)-R\s+1080(\s|$)' -or
        $p.CommandLine -notmatch '(\s|^)-p\s+2222(\s|$)') { return $null }
    return $p
}
function StopOwnedSSH() {
    $pidfile = Join-Path $script:state 'ssh.pid'
    $portfile = Join-Path $script:state 'adb-port.txt'
    $p = ProcessForOwnedSSH
    if ($p) {
        # Do not erase ownership evidence until the verified ssh.exe has
        # actually exited. A failed Stop-Process must never be logged as
        # a completed STOP while an orphan tunnel is still running.
        try {
            Stop-Process -Id ([int]$p.ProcessId) -ErrorAction Stop
            $stopped = $false
            for ($attempt = 0; $attempt -lt 10; $attempt++) {
                $remaining = Get-CimInstance Win32_Process -Filter ("ProcessId={0}" -f $p.ProcessId) -ErrorAction SilentlyContinue
                if (-not $remaining -or
                    ($remaining.Name -eq 'ssh.exe' -and $remaining.CreationDate -ne $p.CreationDate)) {
                    $stopped = $true
                    break
                }
                Start-Sleep -Milliseconds 250
            }
            if (-not $stopped) { Fail 'Verified SSH process did not exit after STOP.' }
            Log 'Stopped and verified connector-owned Windows SSH process.'
        } catch {
            Log ("SSH stop verification failed: " + $_.Exception.Message)
            Fail 'Could not verify termination of connector-owned SSH tunnel; ownership record retained.'
        }
    } elseif (Test-Path -LiteralPath $pidfile) {
        # The PID can be stale or reused. Ignore markers for exited processes,
        # but never discard a live SSH PID that we cannot prove is ours.
        $text = (Get-Content -LiteralPath $pidfile -Raw).Trim()
        if ($text -match '^[0-9]+$') {
            $suspect = Get-CimInstance Win32_Process -Filter "ProcessId=$text" -ErrorAction SilentlyContinue
            if ($suspect -and $suspect.Name -eq 'ssh.exe') {
                Fail 'SSH PID refers to a live process whose ownership cannot be verified; refusing to kill or forget it.'
            }
        }
        Log 'Discarding stale connector SSH ownership marker; no matching live SSH process.'
    }
    Remove-Item -LiteralPath $pidfile -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $portfile -Force -ErrorAction SilentlyContinue
}
function StopOwnedAdb() {
    # Current releases use shared 5037. Old sessions may still be pinned to
    # private 5056, but the new process has no reliable proof of ownership
    # of that listening ADB daemon. Do not kill either server automatically.
    if ($script:port -eq '5056') {
        Log 'Legacy ADB port 5056 preserved; ownership of that server is not independently verified.'
    } else {
        Log 'Shared ADB port 5037 preserved for other applications.'
    }
}
function CommandPath([string]$Name) {
    $r = Get-Command $Name -ErrorAction SilentlyContinue
    if (-not $r) { Fail ("Missing Windows OpenSSH Client component: " + $Name) }
    return $r.Source
}
function InstallKey() {
    $key = Join-Path $script:state 'rg35xx_usb_gui_ed25519'
    if (-not (Test-Path -LiteralPath $key) -or -not (Test-Path -LiteralPath "$key.pub")) {
        if ((Test-Path -LiteralPath $key) -or (Test-Path -LiteralPath "$key.pub")) {
            Fail 'Incomplete connector SSH keypair; refusing to overwrite existing private key.'
        }
        Log 'Creating dedicated SSH ed25519 identity.'
        $sshgen = CommandPath 'ssh-keygen.exe'
        $info = New-Object Diagnostics.ProcessStartInfo
        $info.FileName = $sshgen
        $info.Arguments = ('-q -t ed25519 -N "" -C "rg35xx-usb-gui" -f "{0}"' -f $key)
        $info.UseShellExecute = $false
        $info.CreateNoWindow = $true
        $p = [Diagnostics.Process]::Start($info)
        if (-not $p.WaitForExit(20000)) {
            try { $p.Kill() } catch { }
            Fail 'SSH key generation timed out.'
        }
        if ($p.ExitCode -ne 0) { Fail 'SSH key generation failed.' }
    }
    $pub = (Get-Content -LiteralPath "$key.pub" -Raw).Trim()
    if ($pub -notmatch '^ssh-ed25519 [A-Za-z0-9+/=]+ rg35xx-usb-gui$') {
        Fail 'SSH public key has an unexpected format.'
    }
    # Mismatched private/public keys otherwise fail silently at the SSH server.
    # Compare the public part derived from the private key, preserving both.
    $sshgen = CommandPath 'ssh-keygen.exe'
    $derived = & $sshgen -y -f $key 2>&1
    if ($LASTEXITCODE -ne 0) {
        Fail 'Connector private SSH key cannot be read; no credentials were modified.'
    }
    $derivedParts = (($derived | Out-String).Trim() -split '\s+')
    $publicParts = ($pub -split '\s+')
    if ($derivedParts.Count -lt 2 -or
        $derivedParts[0] -ne $publicParts[0] -or
        $derivedParts[1] -ne $publicParts[1]) {
        Fail 'Connector SSH public/private keys differ; no credentials were overwritten.'
    }
    [void](MustAdb @('shell',"mkdir -p $script:base") 'create device directory')
    Push (Join-Path $script:root 'device/setup-ssh-key.sh') "$script:base/setup-ssh-key.sh"
    [void](MustAdb @('shell',"chmod 755 $script:base/setup-ssh-key.sh") 'chmod key installer')
    $registration = RunAdb @('shell',"sh $script:base/setup-ssh-key.sh '$pub'")
    if ($registration.Code -ne 0 -or $registration.Text -notmatch '(?m)^RGUSB_SSH_KEY_READY\s*$') {
        # ADB exit status by itself is not proof that the remote installer ran.
        Fail ("Device SSH public-key registration could not be verified: " + $registration.Text)
    }
    Log 'Device SSH public key registration verified.'
    return $key
}
function StartSSH() {
    if (CheckSocks) {
        if (-not (ProcessForOwnedSSH)) {
            Fail 'A SOCKS proxy is already active but not owned by this connector. Refusing to take it over.'
        }
        Log 'Reusing connector-owned SSH tunnel.'
        return
    }
    StopOwnedSSH
    $key = $null
    try {
        $key = InstallKey
    } catch {
        # KNULLI supports password authentication too. An unavailable key
        # registration must not block this known-good firmware login method.
        Log ("SSH public-key setup unavailable; trying password authentication: " + $_.Exception.Message)
    }
    $ssh = CommandPath 'ssh.exe'
    $askpassExe = Join-Path (Split-Path -Parent $script:root) 'RG35XXUsbInternetConnector.exe'
    Need $askpassExe
    $known = Join-Path $script:state 'known_hosts'
    $sshlog = Join-Path $script:state 'ssh-client.log'
    # Reset previous attempts so the private log describes this connection only.
    [IO.File]::WriteAllText($sshlog,'',(New-Object Text.UTF8Encoding($false)))
    $info = New-Object Diagnostics.ProcessStartInfo
    $info.FileName = $ssh
    $keyArg = if ($key) { ('-i "{0}"' -f $key) } else { '' }
    # Try the connector-owned key first; if KNULLI rejects it, askpass
    # supplies one automatic "linux" password response. The password is
    # never written to the SSH command line or any file.
    $info.Arguments = ('-v -N -T -o BatchMode=no -o NumberOfPasswordPrompts=1 -o IdentitiesOnly=yes -o PreferredAuthentications=publickey,password -o PasswordAuthentication=yes -o KbdInteractiveAuthentication=no -o ExitOnForwardFailure=yes -o StrictHostKeyChecking=accept-new -o UserKnownHostsFile="{0}" -o ServerAliveInterval=15 -o ServerAliveCountMax=3 -o ConnectTimeout=10 -E "{1}" {2} -R 1080 -p 2222 root@127.0.0.1' -f $known,$sshlog,$keyArg)
    $info.UseShellExecute = $false
    $info.CreateNoWindow = $true
    $info.WindowStyle = [Diagnostics.ProcessWindowStyle]::Hidden
    # OpenSSH invokes our own published WinExe as SSH_ASKPASS; no third-party
    # sshpass or external credential helper is needed. Windows OpenSSH 8.6+
    # requires SSH_ASKPASS_REQUIRE=force for noninteractive prompts.
    $info.EnvironmentVariables['SSH_ASKPASS'] = $askpassExe
    $info.EnvironmentVariables['SSH_ASKPASS_REQUIRE'] = 'force'
    $info.EnvironmentVariables['RG35XX_USB_SSH_ASKPASS'] = '1'
    $info.EnvironmentVariables['DISPLAY'] = 'localhost:0'
    $p = [Diagnostics.Process]::Start($info)
    [IO.File]::WriteAllText((Join-Path $script:state 'ssh.pid'),[string]$p.Id)
    $script:sshStartedHere = $true
    # A fixed 2-second sleep before each 8-second curl could make tunnel
    # setup appear frozen. Probe immediately with a short timeout, bounded
    # by one absolute deadline regardless of individual command duration.
    $deadline = [DateTime]::UtcNow.AddSeconds(60)
    $lastProgress = [DateTime]::UtcNow
    while ([DateTime]::UtcNow -lt $deadline) {
        if ($p.HasExited) { break }
        if (CheckSocks) {
            [IO.File]::WriteAllText((Join-Path $script:state 'adb-port.txt'),$script:port)
            # Report the authentication mechanism, not the credential. This
            # makes it easy to confirm whether password fallback was used.
            if (Test-Path -LiteralPath $sshlog) {
                $authLog = Get-Content -LiteralPath $sshlog -Raw -ErrorAction SilentlyContinue
                if ($authLog -match '(?:using "password"|Authentication succeeded \(password\))') {
                    Log 'SSH authentication succeeded via automatic KNULLI default password.'
                } elseif ($authLog -match '(?:using "publickey"|Authentication succeeded \(publickey\))') {
                    Log 'SSH authentication succeeded via connector-owned public key.'
                }
            }
            Log 'Background SSH reverse SOCKS tunnel verified.'
            return
        }
        if ([DateTime]::UtcNow -ge $lastProgress.AddSeconds(12)) {
            Log 'Waiting for SSH reverse SOCKS tunnel to become ready.'
            $lastProgress = [DateTime]::UtcNow
        }
        Start-Sleep -Milliseconds 700
    }
    StopOwnedSSH
    # Auth diagnostics are stored privately. Classify the common case in the
    # GUI without exposing the public key, serial or private SSH material.
    if (Test-Path -LiteralPath $sshlog) {
        $sshReason = Get-Content -LiteralPath $sshlog -Raw -ErrorAction SilentlyContinue
        if ($sshReason -match 'Permission denied \(publickey(?:,password)?\)') {
            Fail 'KNULLI SSH authentication rejected both configured methods (owned key and default firmware password). Confirm SSH is enabled and the root password is still the default; see the private ssh-client.log.'
        }
        if ($sshReason -match 'remote port forwarding failed') {
            Fail 'KNULLI SSH server rejected reverse SOCKS forwarding on port 1080.'
        }
    }
    Fail ("SSH tunnel failed. Review the private diagnostic file: " + $sshlog)
}
function Deploy() {
    Log 'Deploying audited device scripts and embedded ARM64 executables.'
    [void](MustAdb @('shell',"mkdir -p $script:base") 'create device folder')
    $files = @(
        @((Join-Path $script:root 'bin/daemon-launch-arm64'),"$script:base/daemon-launch"),
        @((Join-Path $script:root 'bin/dns-bridge-arm64'),"$script:base/dns-bridge"),
        @((Join-Path $script:root 'bin/tunctl-arm64'),"$script:base/tunctl"),
        @((Join-Path $script:root 'bin/tun2socks-arm64'),"$script:base/tun2socks"),
        @((Join-Path $script:root 'device/start-fullnet.sh'),"$script:base/start-fullnet.sh"),
        @((Join-Path $script:root 'device/stop-fullnet.sh'),"$script:base/stop-fullnet.sh"),
        @((Join-Path $script:root 'device/watch-fullnet.sh'),"$script:base/watch-fullnet.sh"),
        @((Join-Path $script:root 'device/check-fullnet.sh'),"$script:base/check-fullnet.sh"),
        @((Join-Path $script:root 'device/compat-fullnet.sh'),"$script:base/compat-fullnet.sh"),
        @((Join-Path $script:root 'device/setup-ssh-key.sh'),"$script:base/setup-ssh-key.sh")
    )
    foreach ($pair in $files) { Push $pair[0] $pair[1] }
    [void](MustAdb @('shell',"chmod 755 $script:base/tun2socks $script:base/dns-bridge $script:base/tunctl $script:base/daemon-launch $script:base/*.sh") 'chmod device files')
}
function ApplyCompatibility() {
    $r = RunAdb @('shell',"sh $script:base/compat-fullnet.sh apply")
    if ($r.Text) { Log $r.Text }
    if ($r.Code -ne 0) { Fail 'KNULLI IP or PortMaster compatibility patch failed.' }
}
function StartInternet() {
    $active = Exists "$script:base/active"
    $tun = TunExists
    if ($active -and (CheckNative)) {
        Log 'Existing USB native HTTPS connection is healthy.'
        Log 'RGUSB_NATIVE_HTTPS_OK: direct HTTPS verified on KNULLI.'
        Push (Join-Path $script:root 'device/compat-fullnet.sh') "$script:base/compat-fullnet.sh"
        [void](MustAdb @('shell',"chmod 755 $script:base/compat-fullnet.sh") 'chmod compatibility')
        ApplyCompatibility
        $script:connectionHealthy = $true
        return
    }
    if ($active -or $tun) {
        if ($tun -and -not (Exists "$script:base/tun.created")) {
            Fail 'Unowned rgusb0 detected; refusing to remove another application network interface.'
        }
        Log 'Stale USB state found. Attempting owned cleanup before reconnection.'
        PrepareStop
        StopDevice
        StopOwnedSSH
    }
    Log 'Stage USB forwarding: configuring ADB TCP port 2222.'
    [void](MustAdb @('forward','tcp:2222','tcp:22') 'USB SSH port forwarding')
    $utc = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
    [void](MustAdb @('shell',"date -u -s @$utc") 'set device clock')
    Log 'Stage SSH/SOCKS: establishing authenticated reverse tunnel.'
    StartSSH
    Log 'Stage device payload: uploading verified ARM64 binaries and scripts.'
    Deploy
    # KNULLI may have /etc/resolv.conf -> ../tmp/resolv.conf with a missing target.
    $resolver = RunAdb @('shell',
        'if [ "$(readlink /etc/resolv.conf 2>/dev/null)" = "../tmp/resolv.conf" ] && [ ! -e /tmp/resolv.conf ]; then touch /tmp/resolv.conf; fi')
    if ($resolver.Code -ne 0) { Fail 'Cannot prepare KNULLI volatile resolver target.' }
    Log 'Stage TUN/DNS: starting device TUN interface and DNS-over-HTTPS.'
    Log 'Starting device TUN and DNS-over-HTTPS.'
    $result = RunAdb @('shell',"sh $script:base/start-fullnet.sh")
    if ($result.Text) { Log $result.Text }
    if ($result.Code -ne 0) { Fail 'KNULLI TUN/DNS startup failed.' }
    Log 'Stage native HTTPS: validating DNS, route and direct HTTPS without proxy settings.'
    $ok = $false
    for ($attempt=0; $attempt -lt 3; $attempt++) {
        if (CheckNative) { $ok = $true; break }
        Start-Sleep -Seconds 3
    }
    if (-not $ok) {
        try { StopDevice } catch { Log ("Recovery warning: " + $_.Exception.Message) }
        Fail 'Native DNS and HTTPS verification failed after TUN startup.'
    }
    # A failed optional compatibility step must not erase observed native
    # Internet success. GUI consumes this proof separately from the exit code.
    Log 'RGUSB_NATIVE_HTTPS_OK: direct HTTPS verified on KNULLI.'
    Log 'Stage compatibility: applying KNULLI IP recognition and PortMaster startup guards.'
    ApplyCompatibility
    $script:connectionHealthy = $true
    Log 'Connected: native HTTPS, IP display and PortMaster compatibility applied.'
}
function ShowStatus() {
    if (-not (Exists "$script:base/check-fullnet.sh")) {
        Log 'Device has no installed USB Internet status script.'
        return
    }
    $r = RunAdb @('shell',"sh $script:base/check-fullnet.sh")
    if ($r.Text) { Log $r.Text }
    if ($r.Code -ne 0) { Fail 'Device status check failed.' }
    if ((Exists "$script:base/active") -and -not (CheckNative)) {
        Fail 'Device has a stale active marker but native HTTPS is unavailable.'
    }
}
try {
    Log ("USB Internet action started: " + $Action)
    Discover
    switch ($Action) {
        'start' { StartInternet }
        'status' { ShowStatus }
        'stop' {
            Log 'Stage STOP preparation: verifying owned cleanup script.'
            PrepareStop
            Log 'Stage STOP device: restoring resolver and removing TUN.'
            StopDevice
            Log 'Stage STOP SSH: stopping connector-owned tunnel.'
            StopOwnedSSH
            Log 'Stage STOP ADB: shutting down connector-owned server.'
            StopOwnedAdb
            Log 'STOP completed and verified.'
        }
    }
    Log 'USB Internet action completed successfully.'
    exit 0
}
catch {
    Log ("USB Internet action failed: " + $_.Exception.Message)
    if ($Action -eq 'stop') {
        # On USB unplug, Windows resources can be cleaned but remote network
        # state is UNKNOWN; never report successful remote disconnect.
        StopOwnedSSH
        StopOwnedAdb
        Log 'Windows SSH/ADB cleanup attempted; remote cleanup remains unverified.'
        exit 2
    }
    if ($script:sshStartedHere -and -not $script:connectionHealthy) {
        $remoteActive = $false
        try {
            if ($script:serial) { $remoteActive = Exists "$script:base/active" }
        } catch { }
        if (-not $remoteActive) { StopOwnedSSH }
    }
    exit 1
}
