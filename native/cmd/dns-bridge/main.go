// dns-bridge is a locally bound DNS-over-HTTPS relay for KNULLI USB SOCKS tunnels.
// It speaks UDP and TCP DNS and forwards DoH through an SSH SOCKS5 proxy.
// Migrated from the original v1.4 prototype; standard library only.
package main

import (
    "bytes"
    "context"
    "encoding/binary"
    "errors"
    "flag"
    "fmt"
    "io"
    "log"
    "net"
    "net/http"
    "net/url"
    "os"
    "strings"
    "sync"
    "time"
)

var (
    listen = flag.String("listen", "127.0.0.1:53", "Local DNS UDP/TCP address")
    socks = flag.String("socks", "127.0.0.1:1080", "Local SOCKS5 proxy address")
    doh = flag.String("doh", "https://cloudflare-dns.com/dns-query", "DNS-over-HTTPS endpoint")
)

func socksDial(ctx context.Context, proxy, addr string) (net.Conn, error) {
    d := &net.Dialer{Timeout: 8 * time.Second}
    c, err := d.DialContext(ctx, "tcp", proxy)
    if err != nil { return nil, err }
    _ = c.SetDeadline(time.Now().Add(12 * time.Second))
    fail := func(e error) (net.Conn, error) { _ = c.Close(); return nil, e }
    if _, err = c.Write([]byte{5, 1, 0}); err != nil { return fail(err) }
    rep := make([]byte, 2)
    if _, err = io.ReadFull(c, rep); err != nil { return fail(err) }
    if rep[0] != 5 || rep[1] != 0 {
        return fail(fmt.Errorf("SOCKS5 authentication unavailable (%v)", rep))
    }
    host, portstr, err := net.SplitHostPort(addr)
    if err != nil { return fail(err) }
    p, err := net.LookupPort("tcp", portstr)
    if err != nil { return fail(err) }
    req := []byte{5, 1, 0}
    ip := net.ParseIP(host)
    if ip4 := ip.To4(); ip4 != nil {
        req = append(req, 1)
        req = append(req, ip4...)
    } else if ip6 := ip.To16(); ip6 != nil {
        req = append(req, 4)
        req = append(req, ip6...)
    } else {
        if len(host) == 0 || len(host) > 255 {
            return fail(errors.New("invalid SOCKS hostname"))
        }
        req = append(req, 3, byte(len(host)))
        req = append(req, []byte(host)...)
    }
    req = append(req, byte(p>>8), byte(p))
    if _, err = c.Write(req); err != nil { return fail(err) }
    hdr := make([]byte, 4)
    if _, err = io.ReadFull(c, hdr); err != nil { return fail(err) }
    if hdr[0] != 5 || hdr[1] != 0 {
        return fail(fmt.Errorf("SOCKS connect failure (code %d)", hdr[1]))
    }
    var toRead int
    switch hdr[3] {
    case 1:
        toRead = 4
    case 4:
        toRead = 16
    case 3:
        b := make([]byte, 1)
        if _, err = io.ReadFull(c, b); err != nil { return fail(err) }
        toRead = int(b[0])
    default:
        return fail(errors.New("unknown SOCKS address format"))
    }
    if _, err = io.CopyN(io.Discard, c, int64(toRead+2)); err != nil { return fail(err) }
    if err = c.SetDeadline(time.Time{}); err != nil { return fail(err) }
    return c, nil
}

type bridge struct {
    client *http.Client
    url string
    logMu sync.Mutex
    lastLog time.Time
}

func (b *bridge) query(question []byte) []byte {
    if len(question) < 12 { return nil }
    ctx, cancel := context.WithTimeout(context.Background(), 12*time.Second)
    defer cancel()
    req, err := http.NewRequestWithContext(ctx, "POST", b.url, bytes.NewReader(question))
    if err != nil { b.errorLog(err); return servfail(question) }
    req.Header.Set("Content-Type", "application/dns-message")
    req.Header.Set("Accept", "application/dns-message")
    resp, err := b.client.Do(req)
    if err != nil { b.errorLog(err); return servfail(question) }
    defer resp.Body.Close()
    if resp.StatusCode != 200 {
        b.errorLog(fmt.Errorf("DoH HTTP %d", resp.StatusCode))
        return servfail(question)
    }
    body, err := io.ReadAll(io.LimitReader(resp.Body, 65536))
    if err != nil || len(body) < 12 || len(body) > 65535 {
        b.errorLog(fmt.Errorf("invalid DoH response (%d bytes): %v", len(body), err))
        return servfail(question)
    }
    if !bytes.Equal(body[:2], question[:2]) {
        b.errorLog(errors.New("DNS transaction ID mismatch"))
        return servfail(question)
    }
    return body
}
func (b *bridge) errorLog(err error) {
    b.logMu.Lock()
    defer b.logMu.Unlock()
    if time.Since(b.lastLog) > 10*time.Second {
        log.Printf("DNS upstream error: %s", err)
        b.lastLog = time.Now()
    }
}
func servfail(req []byte) []byte {
    if len(req) < 12 { return nil }
    res := make([]byte, len(req))
    copy(res, req)
    res[2] |= 0x80
    res[3] = (res[3] & 0xf0) | 2
    for i := 6; i < 12; i++ { res[i] = 0 }
    return res
}
func serveUDP(b *bridge, conn net.PacketConn) {
    sem := make(chan struct{}, 48)
    for {
        raw := make([]byte, 4096)
        n, addr, err := conn.ReadFrom(raw)
        if err != nil { log.Printf("UDP read stopped: %v", err); return }
        if n < 12 { continue }
        raw = raw[:n]
        sem <- struct{}{}
        go func(packet []byte, remote net.Addr) {
            defer func() { <-sem }()
            resp := b.query(packet)
            if len(resp) > 4096 { resp = servfail(packet) }
            if len(resp) > 0 { _, _ = conn.WriteTo(resp, remote) }
        }(raw, addr)
    }
}
func serveTCP(b *bridge, listener net.Listener) {
    for {
        c, err := listener.Accept()
        if err != nil { log.Printf("TCP listen stopped: %v", err); return }
        go func(c net.Conn) {
            defer c.Close()
            _ = c.SetDeadline(time.Now().Add(30 * time.Second))
            hdr := make([]byte, 2)
            for i := 0; i < 20; i++ {
                if _, err := io.ReadFull(c, hdr); err != nil { return }
                n := int(binary.BigEndian.Uint16(hdr))
                if n < 12 || n > 4096 { return }
                q := make([]byte, n)
                if _, err := io.ReadFull(c, q); err != nil { return }
                ans := b.query(q)
                if len(ans) < 12 { return }
                binary.BigEndian.PutUint16(hdr, uint16(len(ans)))
                if _, err := c.Write(hdr); err != nil { return }
                if _, err := c.Write(ans); err != nil { return }
                _ = c.SetDeadline(time.Now().Add(30 * time.Second))
            }
        }(c)
    }
}
func main() {
    flag.Parse()
    u, err := url.Parse(*doh)
    if err != nil || (u.Scheme != "https" && u.Scheme != "http") ||
        !strings.Contains(u.Host, ":") && u.Host == "" {
        log.Fatal("invalid DoH URL")
    }
    t := &http.Transport{DialContext: func(ctx context.Context, _ string, addr string) (net.Conn, error) {
        return socksDial(ctx, *socks, addr)
    }, TLSHandshakeTimeout: 10 * time.Second, MaxIdleConns: 8,
       MaxIdleConnsPerHost: 4, IdleConnTimeout: 60 * time.Second}
    b := &bridge{client: &http.Client{Transport: t, Timeout: 12 * time.Second}, url: *doh}
    uconn, err := net.ListenPacket("udp", *listen)
    if err != nil { log.Fatalf("cannot bind local UDP DNS %s: %v", *listen, err) }
    defer uconn.Close()
    l, err := net.Listen("tcp", *listen)
    if err != nil { log.Fatalf("cannot bind local TCP DNS %s: %v", *listen, err) }
    defer l.Close()
    log.Printf("DNS bridge on %s via SOCKS5 %s -> %s", *listen, *socks, *doh)
    go serveTCP(b, l)
    serveUDP(b, uconn)
    os.Exit(1)
}
