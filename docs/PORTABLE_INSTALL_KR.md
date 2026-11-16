# RG35XX+ (2024) KNULLI USB Internet Connector — 사용 안내

> 현재 GitHub Actions 개발용 버전이며 실제 RG35XX+ 기기 테스트 중입니다. GitHub 정식 릴리스가 아닙니다.

## 실행

1. 다운로드한 ZIP 파일을 빈 폴더에 압축 해제합니다.
2. 폴더 내 `RG35XXUsbInternetConnector.exe`를 실행합니다.
3. 최초 실행 시 언어(한국어 / English / 日本語)를 선택합니다.
4. Google Android SDK Platform-Tools 라이선스를 확인하고 동의하면 자동 설치됩니다.
5. Google 공식 `platform-tools`는 **EXE와 같은 폴더**의 `platform-tools/` 아래로 설치됩니다.
6. KNULLI에서 USB ADB 모드 및 SSH를 켜고 **root 비밀번호를 `linux`로 설정**한 뒤 데이터 케이블로 PC와 연결합니다.
7. 프로그램이 기기를 감지하면 가운데 인터넷 연결 버튼을 누릅니다.

## USB SSH 인증 방식 (개발 테스트)

- 현재 개발 빌드는 `root / linux` 비밀번호를 **별도 입력 창 없이 자동 전달**합니다. 기존 공개키 인증의 권한 문제를 회피하기 위한 변경입니다.
- SSH 연결과 역방향 SOCKS 터널은 보이지 않는 백그라운드 프로세스로 유지되고, **인터넷 연결 해제** 버튼에서 정리합니다.
- 비밀번호는 SSH 명령행 인수나 연결 로그에 넣지 않고 인증 도우미에 자식 프로세스 환경변수로만 전달합니다.
- 고정된 기본 비밀번호는 보안상 안전하지 않으므로 신뢰할 수 있는 PC와 USB 연결에서만 사용하세요. SSH를 다른 네트워크에 노출하지 않는 것이 좋습니다.
- 이 방식은 실제 KNULLI 기기에서 연결·해제·재연결 테스트가 끝나기 전까지 **검증되지 않은 개발 기능**입니다.

## 폴더 구조

```text
RG35XX-USB-Internet-Connector/
├─ RG35XXUsbInternetConnector.exe
├─ README_KR.md
├─ backend/
│  ├─ USB-FullInternet.ps1
│  ├─ bin/
│  ├─ device/
│  ├─ third_party/
│  └─ SHA256SUMS
├─ platform-tools/                # 처음 실행할 때 Google에서 자동 생성
└─ data/
   ├─ settings.json               # 언어 설정(일반 텍스트)
   ├─ initialization-complete.marker
   └─ google-sdk-license-accepted.marker
```

**주의:** 보안상 SSH 개인키와 상세 연결 로그는 Windows 사용자별 `%LOCALAPPDATA%`에 별도로 보관합니다. `backend`의 실행 코드 및 스크립트는 GitHub에서 수정할 수 있지만, 다운로드한 빌드의 파일을 직접 변경하면 무결성 검사에 실패합니다. **소스를 수정했다면 CI에서 새 ZIP을 빌드**하는 것이 권장됩니다.

## 진단

앱 화면에는 기기 연결 상태가 표시됩니다. 연결되지 않으면 아래 영어 로그에서 ADB 인식 및 권한 상태를 확인합니다. KNULLI USB 모드가 ADB인지, Windows가 해당 USB 장치를 인식하는지 확인하세요. 다른 도구에서 ADB를 이미 사용하고 있다면 앱이 기본 포트 5037로도 탐색할 수 있습니다.

이 빌드는 기존 v1.4 기반 연결 성공과는 별개로, 아직 새 GUI 전체의 실기기 테스트가 완료되지 않았습니다. 문제가 있으면 GitHub Issue #4에 영어 로그를 첨부하되 개인 경로/ADB 시리얼/SSH 비밀키는 제외하세요.
