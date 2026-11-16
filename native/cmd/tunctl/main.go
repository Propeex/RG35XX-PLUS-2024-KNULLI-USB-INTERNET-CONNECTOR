// tunctl creates/deletes a transiently persistent TUN interface on Linux.
// No dependencies or external ip tuntap features required.
package main

import (
    "encoding/binary"
    "fmt"
    "os"
    "syscall"
    "unsafe"
)

const (
    tunSetIFF     = 0x400454ca
    tunSetPersist = 0x400454cb
    ifTun         = 0x0001
    ifNoPI        = 0x1000
)

func main() {
    if len(os.Args) != 3 || (os.Args[1] != "create" && os.Args[1] != "delete") {
        fmt.Fprintln(os.Stderr, "usage: tunctl create|delete <interface>")
        os.Exit(2)
    }
    name := os.Args[2]
    if len(name) < 1 || len(name) > 15 {
        fmt.Fprintln(os.Stderr, "invalid interface name")
        os.Exit(2)
    }
    for _, c := range name {
        if !((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') ||
             (c >= '0' && c <= '9') || c == '_' || c == '-') {
            fmt.Fprintln(os.Stderr, "invalid interface character")
            os.Exit(2)
        }
    }
    f, err := os.OpenFile("/dev/net/tun", os.O_RDWR, 0)
    if err != nil { fmt.Fprintln(os.Stderr, err); os.Exit(1) }
    defer f.Close()
    var ifr [40]byte // Linux aarch64 struct ifreq: 16-byte name and 24-byte union
    copy(ifr[:16], []byte(name))
    binary.LittleEndian.PutUint16(ifr[16:18], ifTun|ifNoPI)
    _, _, errno := syscall.Syscall(syscall.SYS_IOCTL, f.Fd(), tunSetIFF, uintptr(unsafe.Pointer(&ifr[0])))
    if errno != 0 { fmt.Fprintln(os.Stderr, "TUNSETIFF:", errno); os.Exit(1) }
    var persist uintptr = 1
    if os.Args[1] == "delete" { persist = 0 }
    _, _, errno = syscall.Syscall(syscall.SYS_IOCTL, f.Fd(), tunSetPersist, persist)
    if errno != 0 { fmt.Fprintln(os.Stderr, "TUNSETPERSIST:", errno); os.Exit(1) }
    fmt.Printf("TUN %s %s OK\n", name, os.Args[1])
}
