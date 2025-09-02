# LIS-Forklift-IoT (WATA.LIS)용 Copilot 지침

목적: 지게차 IoT UI와 디바이스 오케스트레이션을 담당하는 .NET 8 WPF Prism 모듈러 앱에서 AI 에이전트가 바로 생산적으로 일하도록 돕습니다.

## 언어/커밋 정책
- Copilot은 기본적으로 한국어로 답변합니다. 필요 시 기술 용어(예: WPF, Prism, EventAggregator)는 영어 병기 허용.
- GitHub 커밋 메시지는 한국어를 사용하되 자연스러운 영어 단어 혼용을 허용합니다. 예: "feat: Display TCPServer init", "fix: RestAPI timeout 처리".
- 커밋 전 반드시 전체 빌드를 먼저 수행하고 오류가 없을 때만 커밋합니다(솔루션 기준 빌드 성공 필수). 경고는 검토 후 필요 시 수정·커밋.

## 전체 구조
- Shell 앱: `WATA.LIS/WATA.LIS` (TargetFramework: `net8.0-windows10.0.18362.0`, Prism.DryIoc WPF)
- Core 라이브러리: `WATA.LIS/WATA.LIS.Core` (이벤트, 인터페이스, 모델, 로깅, 파서, MVVM 기반, Region 이름)
- 기능 모듈: `WATA.LIS/Modules/**`에 Prism IModule로 로드
  - Sensors: Distance, Weight, UHF_RFID, NAV, LIVOX
  - Indicators: LED, DISPLAY
  - Vision: VISION.CAM
  - IF.BE: 백엔드 REST 연동
  - Main: 메인 UI 구성
- 앱 시작(`App.xaml.cs`):
  - `SystemJsonConfigParser`로 설정 로드 후 `I*Model` Singleton 등록
  - `MainConfigModel.device_type`에 따라 `IStatusService` 구현 매핑(CALT, Pantos, DPS 등)
  - `IModuleCatalog`에 모듈 추가 및 `HeartbeatService` 시작

## UI 구성/네비게이션
- Region: `WATA.LIS.Core/RegionNames.cs` 정의, `Views/MainWindow.xaml`에서 사용
- 모듈: `containerRegistry.RegisterForNavigation<View>(RegionNames.*)` 등록, 필요 시 `_regionManager.RequestNavigate(...)` 호출 (예: `Modules/WATA.LIS.Main/MainModule.cs`)
- 중앙 영역 선택: `MainWindowViewModel.MainRegionName`이 `device_type == "DPS"`면 `Content_DPS`, 아니면 `Content_Main`

## 설정/로깅
- 실행 시 설정 경로: `<WorkingDir>\\SystemConfig\\SystemConfig.json` (참조: `Core/Parser/SystemJsonConfigParser.cs`)
- Log4Net 설정: `<WorkingDir>\\Log4Net_WATA.xml` (참조: `Core/Common/Tools.cs`), 실행 파일과 같은 위치 필요
- 로깅 사용: `Tools.Log(message, Tools.ELogType.XXX)` 카테고리 기반 + UI 로그 버퍼 동기화

## 컴포넌트 간 통신
- Prism EventAggregator를 메시지 버스로 사용(pub/sub)
  - 백엔드 REST: `IF.BE/REST/RestAPI.cs`가 `RestClientPostEvent`, `RestClientGetEvent` 구독 → HTTP 호출, 결과/상태는 `BackEndStatusEvent`로 게시
  - 센서/비전/상태 서비스: `Core/Events/**`, `Core/Services/ServiceImpl/*`의 이벤트 사용
- 패턴: 생성자에서 `IEventAggregator` 주입, `Subscribe(..., ThreadOption.BackgroundThread, true)`, 게시 시 `_eventAggregator.GetEvent<TEvent>().Publish(payload)`

## 외부 연동
- 백엔드 HTTP: `RestAPI`가 `HttpWebRequest`/`HttpClient` 사용(개발용 SSL 완화 포함)
- Display: `INDICATOR.DISPLAY/DISPLAYModule.cs`가 `display_enable != 0`이면 `TcpServerSimple` 시작
- Vision: OpenCvSharp + NetMQ, Python DepthAI 스트리밍 서버는 `Modules/WATA.LIS.VISION.CAM/MQTT/README.md` 참고
- Heartbeat: `Modules/WATA.LIS.Heartbeat.Services/HeartbeatService.cs`가 주기적으로 `http://localhost:8080/heartbeat` POST

## 빌드/실행/디버그
- 솔루션: `WATA.LIS/WATA.LIS.sln` (Windows, Visual Studio 권장) / 메인 WPF: `WATA.LIS/WATA.LIS.csproj`
- 구성: `Debug`, `Release`, `Remote`
- 최초 실행 전: NuGet 복원, 작업 디렉터리에 `SystemConfig/` 폴더와 `Log4Net_WATA.xml` 위치 확인

### 빌드 체크 가이드 (커밋 전 필수)
- Visual Studio
  - 솔루션 열기 → 상단 메뉴 Build > Build Solution(Ctrl+Shift+B)
  - 구성(예: Release) 선택 후 전체 솔루션 빌드 성공 확인(에러 0)
- CLI(PowerShell)
  - `dotnet restore .\WATA.LIS\WATA.LIS.sln`
  - `dotnet build .\WATA.LIS\WATA.LIS.sln -c Release`
- 빠른 스모크 체크
  - 출력 경로에 실행 파일과 필수 파일 존재 확인:
    - `WATA.LIS/WATA.LIS/bin/<Config>/net8.0-windows10.0.18362.0/WATA.LIS.exe`
    - 같은 폴더에 `SystemConfig/SystemConfig.json`, `Log4Net_WATA.xml`
- 실패 시 확인 포인트
  - .NET 8 SDK + WindowsDesktop(WPF) 설치 여부
  - NuGet 복원 경로 및 사내 패키지 저장소(필요 시) 접근성
  - 빌드 출력의 첫 에러부터 순차 해결(경고는 검토 후 필요 시 수정)

사전 제안(선택)
- pre-commit 훅/간단 스크립트로 “빌드 통과 시에만 커밋” 자동화 고려
- Post-build 이벤트로 `SystemConfig/`와 `Log4Net_WATA.xml`을 출력 폴더로 복사 설정(실행 편의성)

## 안전하게 기능 추가하기
- 새로운 디바이스/상태 흐름: `Core/Services/ServiceImpl/StatusService_*.cs`에 `IStatusService` 구현, `App.xaml.cs::RegisterTypes`의 `device_type` 매핑 확장
- 새로운 UI View: View/VM 생성 후 해당 모듈에서 `RegionNames.*`로 등록, 필요 시 `IRegionManager`로 네비게이션
- 설정 기반 동작: `Core/Model/SystemConfig/*ConfigModel.cs`에 필드 추가 → `SystemJsonConfigParser`에서 파싱 → 필요한 곳에 `I*Model` 주입 사용

## 참고 파일
- Shell: `WATA.LIS/WATA.LIS/App.xaml`, `App.xaml.cs`, `Views/MainWindow.xaml`, `ViewModels/MainWindowViewModel.cs`
- Core: `WATA.LIS.Core/Common/Tools.cs`, `Parser/SystemJsonConfigParser.cs`, `RegionNames.cs`, `Services/ServiceImpl/*`
- Backend: `Modules/WATA.LIS.IF.BE/REST/RestAPI.cs`
- Modules: `Modules/**/` 내 `*Module.cs`, `Views/`, `ViewModels/`

비고
- 테스트 프로젝트는 없음. 앱 실행과 모듈별 로그로 동작 검증
- 주석/문자열에 한국어 다수. `device_type` 확장 시 기존 명칭 유지 권장
