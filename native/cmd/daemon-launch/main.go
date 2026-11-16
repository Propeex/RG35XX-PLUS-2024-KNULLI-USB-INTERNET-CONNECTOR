// daemon-launch starts one device helper in a new Unix session, detached from
// an ADB shell's terminal and process group. It never modifies boot services.
// Usage: daemon-launch <pidfile> <logfile> <program> [arguments...]
package main

import (
    "fmt"
    "os"
    "os/exec"
    "path/filepath"
    "syscall"
)

func main() {
    if len(os.Args) < 4 {
        fmt.Fprintln(os.Stderr, "usage: daemon-launch <pidfile> <logfile> <program> [args...]")
        os.Exit(2)
    }
    pidfile, logfile, command := os.Args[1], os.Args[2], os.Args[3]
    if !filepath.IsAbs(pidfile) || !filepath.IsAbs(logfile) || !filepath.IsAbs(command) {
        fmt.Fprintln(os.Stderr, "pidfile, logfile, and executable must be absolute paths")
        os.Exit(2)
    }
    f, err := os.OpenFile(logfile, os.O_CREATE|os.O_WRONLY|os.O_TRUNC, 0644)
    if err != nil { fmt.Fprintf(os.Stderr, "open logfile: %v\n", err); os.Exit(1) }
    defer f.Close()
    devnull, err := os.Open(os.DevNull)
    if err != nil { fmt.Fprintf(os.Stderr, "open /dev/null: %v\n", err); os.Exit(1) }
    defer devnull.Close()
    cmd := exec.Command(command, os.Args[4:]...)
    cmd.Stdin = devnull
    cmd.Stdout = f
    cmd.Stderr = f
    cmd.SysProcAttr = &syscall.SysProcAttr{Setsid: true}
    if err := cmd.Start(); err != nil { fmt.Fprintf(os.Stderr, "start helper: %v\n", err); os.Exit(1) }
    if err := os.WriteFile(pidfile, []byte(fmt.Sprintf("%d\n", cmd.Process.Pid)), 0644); err != nil {
        _ = cmd.Process.Kill()
        fmt.Fprintf(os.Stderr, "write pidfile: %v\n", err)
        os.Exit(1)
    }
    fmt.Printf("launched %s pid %d (new session)\n", filepath.Base(command), cmd.Process.Pid)
    _ = cmd.Process.Release()
}
