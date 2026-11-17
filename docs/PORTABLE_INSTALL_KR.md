# RG35XX+ (2024) KNULLI USB Internet Connector — 사용 안내

> **v0.1.0-beta.2 테스트 배포판**입니다. 일반 사용자도 내려받을 수 있지만 실기기 START/STOP 반복 및 USB 재연결 문제는 추가 검증이 필요합니다. 안정판이 아닙니다.

## 실행

1. 다운로드한 ZIP 파일을 빈 폴더에 압축 해제합니다.
2. 폴더 내 **`KNULLI USB Link.exe`를 실행합니다.** `RG35XXUsbInternetConnector.exe`는 내부 .NET 8 실행 파일입니다.
3. 최초 실행 시 언어(한국어 / English / 日本語)를 선택합니다.
4. PC에 **.NET 8 Desktop Runtime**이 없다면 선택한 언어로 안내하고, 동의 시 Microsoft 공식 설치 스크립트로 해당 Windows 사용자 계정에 **한 번만** 설치합니다. 이미 있으면 즉시 다음 단계로 진행합니다.
5. Google Android SDK Platform-Tools 라이선스를 확인하고 동의하면 자동 설치됩니다.
6. Google 공식 `platform-tools`는 **EXE와 같은 폴더**의 `platform-tools/` 아래로 설치됩니다.
7. KNULLI에서 USB ADB 모드 및 SSH를 켜고 데이터 케이블로 PC와 연결합니다.
8. 프로그램이 기기를 감지하면 가운데 인터넷 연결 버튼을 누릅니다.

## 폴더 구조

```text
RG35XX-USB-Internet-Connector/
├─ KNULLI USB Link.exe               # 실행용 작은 런처
├─ RG35XXUsbInternetConnector.exe  # .NET 8 GUI (런타임 미포함)
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

**참고:** .NET 8 Desktop Runtime은 이미 설치된 PC에서는 내려받지 않습니다. 처음 필요할 때만 `%LOCALAPPDATA%\\RG35XXUsbInternetConnector\\dotnet`에 사용자별로 설치합니다. Windows 10/11의 기본 .NET Framework 4.x를 사용하는 작은 런처가 설치 안내를 담당합니다. 자동 연결은 하지 않습니다.

**주의:** 보안상 SSH 개인키와 상세 연결 로그는 Windows 사용자별 `%LOCALAPPDATA%`에 별도로 보관합니다. `backend`의 실행 코드 및 스크립트는 GitHub에서 수정할 수 있지만, 다운로드한 빌드의 파일을 직접 변경하면 무결성 검사에 실패합니다. **소스를 수정했다면 CI에서 새 ZIP을 빌드**하는 것이 권장됩니다.

## 진단

앱 화면에는 기기 연결 상태가 표시됩니다. 연결되지 않으면 아래 영어 로그에서 ADB 인식 및 권한 상태를 확인합니다. KNULLI USB 모드가 ADB인지, Windows가 해당 USB 장치를 인식하는지 확인하세요. 프로그램과 ADB Explorer는 기본적으로 **공용 ADB 포트 5037 하나**를 사용합니다. 프로그램 종료 후 작업 관리자에 `adb.exe` 서버가 계속 보일 수 있으며, 이는 다른 앱과의 연결을 보존하기 위한 정상 동작입니다. 프로그램이 임의로 다른 앱의 ADB 서버를 종료하지 않습니다.

### USB 케이블을 뽑았다 다시 연결했는데 인식되지 않을 때

테스트된 RG35XX+ (2024, Wi-Fi 미탑재)에서는 **USB 케이블 재연결 후 Windows 장치 관리자에서도 기기가 보이지 않는 현상**이 관찰됐습니다. PC 프로그램이나 ADB Explorer 모두 인식하지 못하는 경우가 있으며, 기기에서 기존 **USB 모드를 다시 선택하거나 리셋**하면 재인식된 사례가 있습니다.

- 이는 기기/펌웨어/Windows USB 장치 등록 단계의 호환성 제약으로, 프로그램만으로 자동 해결된다고 보장할 수 없습니다.
- 앱은 **KNULLI 펌웨어나 USB Gadget 설정을 강제로 변경하지 않습니다.** 다만 인터넷 START/STOP 시에는 기존 승인된 임시 네트워크 설정을 사용합니다.
- USB를 뽑거나 Windows가 장치를 인식하지 못하면 앱은 인터넷 연결을 자동으로 다시 시작하지 않습니다.
- 연결 상태가 해제돼도 공용 `adb.exe` 서버가 남는 것은 정상입니다. 다른 프로그램을 보호하기 위해 임의로 종료하지 마세요.

### 실기기 검증 현황

USB 모드를 활성화한 뒤 기기를 감지하는 것과 ADB Explorer와의 공존은 테스트됐습니다. 단 **이 Beta의 정확한 ZIP에서 인터넷 START → STOP → START 반복, 업데이트 및 PC 신규 설치**는 아직 전체 검증되지 않았습니다.

문제가 있으면 GitHub Issue에 **개인 경로·ADB 시리얼·SSH 비밀키를 제거한** 영어 로그만 공유해 주세요.
