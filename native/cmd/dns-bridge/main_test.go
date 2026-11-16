package main

import (
    "bytes"
    "context"
    "encoding/binary"
    "io"
    "net"
    "net/http"
    "net/http/httptest"
    "strconv"
    "testing"
    "time"
)

func TestServfail(t *testing.T) {
    q := make([]byte, 19)
    q[0] = 0xab
    q[1] = 0xcd
    q[2] = 1
    q[5] = 1
    r := servfail(q)
    if len(r) != len(q) || r[0] != q[0] || r[1] != q[1] ||
        r[2]&0x80 == 0 || r[3]&0x0f != 2 {
        t.Fatalf("bad SERVFAIL: %v", r)
    }
}

// Minimal fake SOCKS5 server used only for isolated unit tests.
func fakeSocks(t *testing.T) (string, func()) {
    t.Helper()
    l, err := net.Listen("tcp", "127.0.0.1:0")
    if err != nil { t.Fatal(err) }
    go func() {
        for {
            c, err := l.Accept()
            if err != nil { return }
            go func(c net.Conn) {
                defer c.Close()
                _ = c.SetDeadline(time.Now().Add(10 * time.Second))
                hdr := make([]byte, 3)
                if _, err := io.ReadFull(c, hdr); err != nil { return }
                if !bytes.Equal(hdr, []byte{5, 1, 0}) { return }
                _, _ = c.Write([]byte{5, 0})
                v := make([]byte, 4)
                if _, err := io.ReadFull(c, v); err != nil { return }
                var host string
                switch v[3] {
                case 1:
                    bs := make([]byte, 4)
                    if _, err := io.ReadFull(c, bs); err != nil { return }
                    host = net.IP(bs).String()
                case 3:
                    var n [1]byte
                    if _, err := io.ReadFull(c, n[:]); err != nil { return }
                    bs := make([]byte, int(n[0]))
                    if _, err := io.ReadFull(c, bs); err != nil { return }
                    host = string(bs)
                default:
                    return
                }
                var pp [2]byte
                if _, err := io.ReadFull(c, pp[:]); err != nil { return }
                port := int(binary.BigEndian.Uint16(pp[:]))
                upstream, err := net.DialTimeout("tcp",
                    net.JoinHostPort(host, strconv.Itoa(port)), 3*time.Second)
                if err != nil {
                    _, _ = c.Write([]byte{5, 5, 0, 1, 0, 0, 0, 0, 0, 0})
                    return
                }
                defer upstream.Close()
                _, _ = c.Write([]byte{5, 0, 0, 1, 127, 0, 0, 1, 0, 0})
                _ = c.SetDeadline(time.Time{})
                go io.Copy(upstream, c)
                io.Copy(c, upstream)
            }(c)
        }
    }()
    return l.Addr().String(), func(){ _ = l.Close() }
}

func TestSocksDial(t *testing.T) {
    addr, closeFn := fakeSocks(t)
    defer closeFn()
    upstream, err := net.Listen("tcp", "127.0.0.1:0")
    if err != nil { t.Fatal(err) }
    defer upstream.Close()
    go func() {
        c, err := upstream.Accept()
        if err == nil { defer c.Close(); io.Copy(c, c) }
    }()
    c, err := socksDial(context.Background(), addr, upstream.Addr().String())
    if err != nil { t.Fatal(err) }
    defer c.Close()
    _ = c.SetDeadline(time.Now().Add(3*time.Second))
    _, _ = c.Write([]byte("xyz"))
    var echo [3]byte
    _, err = io.ReadFull(c, echo[:])
    if err != nil || string(echo[:]) != "xyz" {
        t.Fatalf("bad echo: %q %v", echo, err)
    }
}

func TestDoH(t *testing.T) {
    server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, req *http.Request) {
        if req.Method != "POST" || req.Header.Get("Content-Type") != "application/dns-message" {
            t.Errorf("bad headers")
        }
        question, _ := io.ReadAll(req.Body)
        question[2] |= 0x80
        w.Header().Set("Content-Type", "application/dns-message")
        _, _ = w.Write(question)
    }))
    defer server.Close()
    proxy, closeFn := fakeSocks(t)
    defer closeFn()
    transport := &http.Transport{DialContext: func(ctx context.Context, _ string, addr string) (net.Conn, error) {
        return socksDial(ctx, proxy, addr)
    }}
    b := &bridge{client: &http.Client{Transport: transport, Timeout: 3*time.Second}, url: server.URL}
    q := make([]byte, 12)
    q[0] = 33
    q[5] = 1
    r := b.query(q)
    if len(r) != 12 || r[0] != 33 || r[2]&0x80 == 0 {
        t.Fatalf("invalid reply: %v", r)
    }
}
