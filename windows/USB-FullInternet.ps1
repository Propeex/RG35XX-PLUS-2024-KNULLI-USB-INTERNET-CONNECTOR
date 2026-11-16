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
$script:port = if ($env:ADB_SERVER_PORT -match '^[0-9]{4,5}$') { $env:ADB_SERVER_PORT } else { '5056' }
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
function Exists([string]$Remote) {
    $r = RunAdb @('shell',"test -e $Remote")
    return ($r.Code -eq 0)
}
function TunExists() {
    $r = RunAdb @('shell','ip link show rgusb0 >/dev/null 2>&1')
    return ($r.Code -eq 0)
}
function CheckNative() {
    $r = RunAdb @('shell',"curl --noproxy '*' --max-time 15 -sSI https://example.com")
    return ($r.Code -eq 0 -and $r.Text -match '(?m)^HTTP/(?:1\.[01]|2|3)\s+[23][0-9][0-9]')
}
function CheckSocks() {
    $r = RunAdb @('shell','curl --max-time 8 -sSI --socks5-hostname 127.0.0.1:1080 https://example.com')
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
        $r = RunAdb @('shell',"sh $script:base/stop-fullnet.sh")
        if ($r.Text) { Log $r.Text }
        if ($r.Code -ne 0) { Fail 'KNULLI STOP reported incomplete cleanup; no forced deletion attempted.' }
    } elseif ((Exists "$script:base/active") -or (TunExists)) {
        Fail 'Active network artifacts exist, but the owned STOP script is missing.'
    }
    if ((Exists "$script:base/active") -or (TunExists)) {
        Fail 'KNULLI STOP verification failed: active flag or rgusb0 still exists.'
    }
    Log 'KNULLI STOP verified: owned active flag and rgusb0 removed.'
}
function ProcessForOwnedSSH() {
    $pidfile = Join-Path $script:state 'ssh.pid'
    if (-not (Test-Path -LiteralPath $pidfile)) { return $null }
    $text = (Get-Content -LiteralPath $pidfile -Raw).Trim()
    if ($text -notmatch '^[0-9]+$') { return $null }
    $p = Get-CimInstance Win32_Process -Filter "ProcessId=$text" -ErrorAction SilentlyContinue
    if (-not $p -or $p.Name -ne 'ssh.exe') { return $null }
    # Recognize both the former key-based tunnel and the password-based one.
    # The PID file is still required, so unrelated SSH processes are untouched.
    $ownedAuth = ($p.CommandLine -match 'rg35xx_usb_gui_ed25519' -or
        $p.CommandLine -match 'PreferredAuthentications=password')
    if (-not $ownedAuth -or
        $p.CommandLine -notmatch 'root@127\.0\.0\.1' -or
        $p.CommandLine -notmatch '(\s|^)-R\s+1080(\s|$)') { return $null }
    return $p
}
function StopOwnedSSH() {
    $p = ProcessForOwnedSSH
    if ($p) {
        try {
            Stop-Process -Id ([int]$p.ProcessId) -ErrorAction Stop
            Log 'Stopped connector-owned Windows SSH process.'
        } catch {
            Log ("SSH stop warning: " + $_.Exception.Message)
        }
    }
    # Never kill another SSH process due to PID reuse.
    Remove-Item -LiteralPath (Join-Path $script:state 'ssh.pid') -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath (Join-Path $script:state 'adb-port.txt') -Force -ErrorAction SilentlyContinue
}
function StopOwnedAdb() {
    # The standard 5037 daemon may be owned by an IDE or another app.
    # Never stop it. Only port 5056 is connector-managed.
    if ($script:port -ne '5056') {
        Log 'Shared ADB port 5037 preserved; other clients may be using it.'
        return
    }
    try {
        [void](RunAdb @('kill-server'))
        Log 'Requested shutdown of connector-specific ADB server on 5056.'
    } catch {
        Log ("ADB server stop warning: " + $_.Exception.Message)
    }
}
function CommandPath([string]$Name) {
    $r = Get-Command $Name -ErrorAction SilentlyContinue
    if (-not $r) { Fail ("Missing Windows OpenSSH Client component: " + $Name) }
    return $r.Source
}
# KNULLI's SHARE partition can be exFAT, where authorized_keys cannot
# reliably satisfy Dropbear's ownership/permission checks. The connector
# instead uses its bundled, console-free SSH_ASKPASS helper for the documented
# default KNULLI root password. No password is passed in process arguments.
function StartSSH() {
    if (CheckSocks) {
        if (-not (ProcessForOwnedSSH)) {
            Fail 'A SOCKS proxy is already active but not owned by this connector. Refusing to take it over.'
        }
        Log 'Reusing connector-owned SSH tunnel.'
        return
    }
    StopOwnedSSH
    $askpass = Join-Path $script:root 'bin/ssh-askpass.exe'
    Need $askpass
    $ssh = CommandPath 'ssh.exe'
    $known = Join-Path $script:state 'known_hosts'
    $sshlog = Join-Path $script:state 'ssh-client.log'
    $info = New-Object Diagnostics.ProcessStartInfo
    $info.FileName = $ssh
    $info.Arguments = ('-N -T -o BatchMode=no -o NumberOfPasswordPrompts=1 -o PreferredAuthentications=password,keyboard-interactive -o PubkeyAuthentication=no -o ExitOnForwardFailure=yes -o StrictHostKeyChecking=accept-new -o UserKnownHostsFile="{0}" -o ServerAliveInterval=15 -o ServerAliveCountMax=3 -o ConnectTimeout=10 -E "{1}" -R 1080 -p 2222 root@127.0.0.1' -f $known,$sshlog)
    $info.UseShellExecute = $false
    # Use the OpenSSH askpass protocol without opening a CMD or password window.
    # 'force' is required by some Windows OpenSSH builds even without a TTY.
    $info.EnvironmentVariables['SSH_ASKPASS'] = $askpass
    $info.EnvironmentVariables['SSH_ASKPASS_REQUIRE'] = 'force'
    $info.EnvironmentVariables['DISPLAY'] = 'localhost:0'
    $info.CreateNoWindow = $true
    $info.WindowStyle = [Diagnostics.ProcessWindowStyle]::Hidden
    $p = [Diagnostics.Process]::Start($info)
    [IO.File]::WriteAllText((Join-Path $script:state 'ssh.pid'),[string]$p.Id)
    $script:sshStartedHere = $true
    for ($i=0; $i -lt 18; $i++) {
        Start-Sleep -Seconds 2
        if (CheckSocks) {
            [IO.File]::WriteAllText((Join-Path $script:state 'adb-port.txt'),$script:port)
            Log 'Background SSH reverse SOCKS tunnel verified.'
            return
        }
        if ($p.HasExited) { break }
    }
    StopOwnedSSH
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
    [void](MustAdb @('forward','tcp:2222','tcp:22') 'USB SSH port forwarding')
    $utc = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
    [void](MustAdb @('shell',"date -u -s @$utc") 'set device clock')
    StartSSH
    Deploy
    # KNULLI may have /etc/resolv.conf -> ../tmp/resolv.conf with a missing target.
    $resolver = RunAdb @('shell',
        'if [ "$(readlink /etc/resolv.conf 2>/dev/null)" = "../tmp/resolv.conf" ] && [ ! -e /tmp/resolv.conf ]; then touch /tmp/resolv.conf; fi')
    if ($resolver.Code -ne 0) { Fail 'Cannot prepare KNULLI volatile resolver target.' }
    Log 'Starting device TUN and DNS-over-HTTPS.'
    $result = RunAdb @('shell',"sh $script:base/start-fullnet.sh")
    if ($result.Text) { Log $result.Text }
    if ($result.Code -ne 0) { Fail 'KNULLI TUN/DNS startup failed.' }
    $ok = $false
    for ($attempt=0; $attempt -lt 3; $attempt++) {
        if (CheckNative) { $ok = $true; break }
        Start-Sleep -Seconds 3
    }
    if (-not $ok) {
        try { StopDevice } catch { Log ("Recovery warning: " + $_.Exception.Message) }
        Fail 'Native DNS and HTTPS verification failed after TUN startup.'
    }
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
            PrepareStop
            StopDevice
            StopOwnedSSH
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
