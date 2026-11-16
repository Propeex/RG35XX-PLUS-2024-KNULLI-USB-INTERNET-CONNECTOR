package main

import "testing"

func TestIsPasswordPrompt(t *testing.T) {
    cases := []struct{
        prompt string
        want bool
    }{
        {"root@127.0.0.1's password:", true},
        {"Password:", true},
        {"password: ", true},
        {"Enter passphrase for key", false},
        {"root@192.168.1.3's password:", false},
        {"Host authenticity (yes/no)?", false},
        {"", false},
    }
    for _, c := range cases {
        if got := isPasswordPrompt(c.prompt); got != c.want {
            t.Errorf("isPasswordPrompt(%q)=%t, expected %t", c.prompt, got, c.want)
        }
    }
}
