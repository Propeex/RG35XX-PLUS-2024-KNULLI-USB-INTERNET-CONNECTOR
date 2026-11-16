package main

import (
    "fmt"
    "os"
    "strings"
)

func isPasswordPrompt(prompt string) bool {
    p := strings.ToLower(strings.TrimSpace(prompt))
    return strings.Contains(p, "password") &&
        (strings.Contains(p, "root@127.0.0.1") || strings.Trim(p, " :") == "password")
}

func main() {
    if len(os.Args) != 2 || !isPasswordPrompt(os.Args[1]) {
        os.Exit(1)
    }
    value := os.Getenv("RG35XX_SSH_ASKPASS_VALUE")
    if value == "" {
        os.Exit(1)
    }
    if _, err := fmt.Fprintln(os.Stdout, value); err != nil {
        os.Exit(1)
    }
}
