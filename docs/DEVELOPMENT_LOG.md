# Development log (append-only chronological notes)

## 2026-11-16 — Repository initialized
- Confirmed repository `Propeex/RG35XX-PLUS-2024-KNULLI-USB-INTERNET-CONNECTOR` existed and was initially **empty**.
- Development mandate: GitHub is the source of truth; future chats read repo docs/issues/PRs before coding. Maintain full history and explicit test evidence.
- Chosen direction: Windows lightweight GUI EXE; integrated app/Google Platform-Tools updates; visible progress; language menu; live device status; one Connect/Disconnect button; English log.
- Early beta policy; do **not** label historical ZIPs as published GitHub releases. No stable release without physical testing.
- First action: architecture, support scope, roadmap, project context and transparent README.
- Pending: recover/audit legacy source and native helpers; write/compile EXE; verify on Windows and RG35XX hardware.

## Historical findings (pre-repository, summarized)
- KNULLI v1.4 USB TUN/DNS successfully reached external HTTPS without proxy flags. Earlier builds lost helper processes after exiting ADB.
- KNULLI IP UI was NOT CONNECTED despite a working TUN. `rgusb0:eth0` secondary IP label fixed access to downloads.
- PortMaster Python Requests lacked SOCKS support; unsetting inherited proxy variables **only in its launcher** fixed startup, online update and installations.
- Some content downloads failed HTTP 404 but others worked.
- v1.5 auto-recovery was blocked by stale `active` / DNS failure; Windows ADB server could linger and lock the tools folder.
- v1.6 historical script-based WinForms GUI was proposed, but **not yet verified as a working EXE**.
- Legacy build artifacts were outside the GitHub repository. Do not assume they are present in a fresh chat or checkout.
