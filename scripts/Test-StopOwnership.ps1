# Offline PowerShell 5.1 regression checks for owned SSH tunnel cleanup.
# No real ssh.exe, adb.exe, device, or network operations occur here.
$ErrorActionPreference = 'Stop'
$path = Join-Path $PSScriptRoot '../windows/USB-FullInternet.ps1'
$tokens = $null
$errors = $null
$ast = [Management.Automation.Language.Parser]::ParseFile(
    (Resolve-Path -LiteralPath $path).Path, [ref]$tokens, [ref]$errors)
if ($errors.Count -gt 0) { throw 'Backend PowerShell did not parse.' }

foreach ($name in @('ProcessForOwnedSSH', 'StopOwnedSSH', 'StopOwnedAdb')) {
    $wanted = $ast.Find({
        param($node)
        $node -is [Management.Automation.Language.FunctionDefinitionAst] -and
        $node.Name -eq $name
    }, $true)
    if (-not $wanted) { throw "Missing backend safety function: $name" }
    Invoke-Expression $wanted.Extent.Text
}

$script:state = Join-Path $env:TEMP ('rgusb-ssh-stop-test-' + [guid]::NewGuid().ToString('N'))
$script:logs = @()
$script:fakeProcess = $null
$script:killed = 0
$pidfile = Join-Path $script:state 'ssh.pid'
$portfile = Join-Path $script:state 'adb-port.txt'

function Log([string]$Message) { $script:logs += $Message }
function Fail([string]$Message) { throw $Message }
function Get-CimInstance {
    [CmdletBinding()]
    param([string]$ClassName, [string]$Filter)
    if ($ClassName -ne 'Win32_Process') { throw "Unexpected CIM class." }
    return $script:fakeProcess
}
function Stop-Process {
    [CmdletBinding()]
    param([int]$Id)
    $script:killed++
    $script:fakeProcess = $null
}
function RunAdb {
    param([string[]]$Argv)
    throw "Never run ADB from the shutdown safety test."
}

try {
    [void](New-Item -ItemType Directory -Path $script:state -Force)
    # SSH command line includes -E private diagnostics path even in the
    # password-fallback case when there is no -i key argument.
    $script:fakeProcess = [pscustomobject]@{
        ProcessId = 123456
        Name = 'ssh.exe'
        CreationDate = [datetime]'2026-01-01T00:00:00'
        CommandLine = ('ssh.exe -E "{0}" -R 1080 -p 2222 root@127.0.0.1' -f
            (Join-Path $script:state 'ssh-client.log'))
    }
    [IO.File]::WriteAllText($pidfile, '123456')
    [IO.File]::WriteAllText($portfile, '5037')
    if (-not (ProcessForOwnedSSH)) {
        throw 'Owned password-fallback SSH tunnel was not recognized.'
    }

    StopOwnedSSH
    if ($script:killed -ne 1 -or (Test-Path $pidfile) -or (Test-Path $portfile)) {
        throw 'A verified owned tunnel was not stopped and fully cleaned up.'
    }

    # Even an SSH command line with the SAME reverse tunnel arguments is
    # unowned if -E points to someone else's diagnostics file. An unrelated
    # process may reuse the PID previously stored in our marker.
    $script:fakeProcess = [pscustomobject]@{
        ProcessId = 123456
        Name = 'ssh.exe'
        CreationDate = [datetime]'2026-01-03T00:00:00'
        CommandLine = 'ssh.exe -E "C:\OtherProgram\ssh-client.log" -R 1080 -p 2222 root@127.0.0.1'
    }
    [IO.File]::WriteAllText($pidfile, '123456')
    [IO.File]::WriteAllText($portfile, '5037')
    if (ProcessForOwnedSSH) {
        throw 'SSH ownership was granted to an unrelated diagnostics directory.'
    }
    $refused = $false
    try { StopOwnedSSH } catch { $refused = $true }
    if (-not $refused -or $script:killed -ne 1 -or -not (Test-Path $pidfile)) {
        throw 'Unowned SSH with matching arguments was killed or forgotten.'
    }

    # A PID belonging to an unrelated ssh.exe must NOT be killed, and its
    # ownership evidence must NOT silently disappear.
    $script:fakeProcess = [pscustomobject]@{
        ProcessId = 123456
        Name = 'ssh.exe'
        CreationDate = [datetime]'2026-01-02T00:00:00'
        CommandLine = 'ssh.exe -N user@remote-server.example -p 22'
    }
    [IO.File]::WriteAllText($pidfile, '123456')
    [IO.File]::WriteAllText($portfile, '5037')
    $refused = $false
    try { StopOwnedSSH }
    catch { $refused = $true }
    if (-not $refused -or $script:killed -ne 1 -or
        -not (Test-Path $pidfile) -or -not (Test-Path $portfile)) {
        throw 'Unowned live SSH process was killed or its PID record lost.'
    }

    # PID reuse by an unrelated non-SSH process should not be killed.
    $script:fakeProcess.Name = 'notepad.exe'
    StopOwnedSSH
    if ($script:killed -ne 1 -or (Test-Path $pidfile)) {
        throw 'Stale PID reuse handling was unsafe.'
    }

    # Neither legacy isolated nor shared server is ever killed by the
    # new updater; ADB Explorer can depend on the shared 5037 daemon.
    $script:port = '5037'
    StopOwnedAdb
    $script:port = '5056'
    StopOwnedAdb
    if ($script:killed -ne 1) { throw 'Unexpected process kill in ADB cleanup.' }

    Write-Host 'SSH ownership, stale PID, and shared ADB preservation tests passed.'
}
finally {
    Remove-Item -LiteralPath $script:state -Force -Recurse -ErrorAction SilentlyContinue
}
