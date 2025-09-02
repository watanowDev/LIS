using Prism.Ioc;
using Prism.Modularity;
using System;
using System.Windows;
using WATA.LIS.Core.Interfaces;
using WATA.LIS.Core.Model.RFID;
using WATA.LIS.Core.Model.SystemConfig;
using WATA.LIS.Core.Parser;
using WATA.LIS.Core.Services;
using WATA.LIS.IF.BE;
using WATA.LIS.INDICATOR.DISPLAY;
using WATA.LIS.INDICATOR.LED;
using WATA.LIS.Main;
using WATA.LIS.SENSOR.Distance;
using WATA.LIS.SENSOR.NAV;
using WATA.LIS.SENSOR.UHF_RFID;
using WATA.LIS.SENSOR.WEIGHT;
using WATA.LIS.Views;
using WATA.LIS.VISION.CAM;
using WATA.LIS.SENSOR.LIVOX;
using WATA.LIS.Core.Services.ServiceImpl;
using WATA.LIS.Heartbeat.Services;
using Microsoft.Extensions.Logging;
using WATA.LIS.DB;
using WATA.LIS.Core.Common;
using Prism.Events;
using WATA.LIS.Core.Events.WeightSensor;
using WATA.LIS.Core.Events.BackEnd;
using System.Threading.Tasks;

namespace WATA.LIS
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App
    {
        private HeartbeatService _heartbeatService;
    private SessionRepository _sessionRepo;
    private SystemStatusRepository _statusRepo;
    private System.Windows.Threading.DispatcherTimer _statusTimer;
    private WeightRepository _weightRepo;
    private IEventAggregator _eventAggregator;
    private volatile bool _networkOkFromBackend = false; // updated via BackEndStatusEvent

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // DB 마이그레이션 및 세션 시작 기록 (비차단)
            var connectionString = "Host=localhost;Username=postgres;Password=wata2019;Database=forkliftDB"; // TODO: SystemConfig에서 이동 가능
            _ = InitializeDatabaseAsync(connectionString);

            // HeartbeatService 초기화 및 시작
            var logger = Container.Resolve<ILogger<HeartbeatService>>();
            _heartbeatService = new HeartbeatService(logger);
            _heartbeatService.Start();

            // EventAggregator 구독 준비
            _eventAggregator = Container.Resolve<IEventAggregator>();
            // Backend 상태 이벤트를 수신하여 네트워크/백엔드 상태를 추정
            _eventAggregator
                .GetEvent<BackEndStatusEvent>()
                .Subscribe(status =>
                {
                    // status: 1(OK), -1(NG)
                    _networkOkFromBackend = status != -1;
                    // GlobalValue.IS_ERROR.backend 는 다른 곳에서도 갱신되지만, 보수적으로 동기화
                    GlobalValue.IS_ERROR.backend = _networkOkFromBackend;
                }, ThreadOption.BackgroundThread, true);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // 세션 종료 마킹 (fire-and-forget)
            _ = Task.Run(async () =>
            {
                try { if (_sessionRepo != null) await _sessionRepo.MarkEndAsync(GlobalValue.SessionId); }
                catch { }
            });
            base.OnExit(e);
        }

        private async Task InitializeDatabaseAsync(string cs)
        {
            try
            {
                var migrator = new MigrationService(cs);
                await migrator.EnsureSchemaAsync();

                _sessionRepo = new SessionRepository(cs);
                _statusRepo = new SystemStatusRepository(cs);
                await _statusRepo.EnsureTableAsync();
                var machine = Environment.MachineName;
                await _sessionRepo.UpsertStartAsync(GlobalValue.SessionId, GlobalValue.SystemVersion, machine);

                // 주기적 상태 기록 (1초 간격)
                _statusTimer = new System.Windows.Threading.DispatcherTimer();
                _statusTimer.Interval = TimeSpan.FromSeconds(1);
                _statusTimer.Tick += async (_, __) =>
                {
                    try
                    {
                        // 기존 Tools.Log는 유지. 여기서는 상태 스냅샷만 기록.
                        // backend_ok: IS_ERROR.backend 가 true일 때 OK
                        bool backendOk = GlobalValue.IS_ERROR.backend;
                        // network_ok: 백엔드 통신 이벤트 기반 추정 (없으면 backendOk로 대체)
                        bool networkOk = _networkOkFromBackend || backendOk;

                        // setAllReady: 주요 센서/백엔드 OK의 합성
                        bool setAllReady = GlobalValue.IS_ERROR.camera &&
                                           GlobalValue.IS_ERROR.distance &&
                                           GlobalValue.IS_ERROR.rfid &&
                                           GlobalValue.IS_ERROR.backend;

                        // 오류 코드/메시지 구성 (미정의면 null)
                        string errorCode = null;
                        string message = null;
                        if (!setAllReady)
                        {
                            var notReady = new System.Collections.Generic.List<string>();
                            if (!GlobalValue.IS_ERROR.camera) notReady.Add("camera");
                            if (!GlobalValue.IS_ERROR.distance) notReady.Add("distance");
                            if (!GlobalValue.IS_ERROR.rfid) notReady.Add("rfid");
                            if (!GlobalValue.IS_ERROR.backend) notReady.Add("backend");
                            errorCode = string.Join(",", notReady);
                            message = notReady.Count > 0 ? ($"not ready: {string.Join(", ", notReady)}") : null;
                        }

                        await _statusRepo.InsertAsync(backendOk, networkOk, setAllReady, errorCode, message, GlobalValue.SessionId);
                    }
                    catch { }
                };
                _statusTimer.Start();

                // weight_reading 테이블 준비 및 구독 연결
                _weightRepo = new WeightRepository(cs);
                await _weightRepo.EnsureTableAsync();
                if (_eventAggregator != null)
                {
                    _eventAggregator
                        .GetEvent<WeightSensorEvent>()
                        .Subscribe(async model =>
                        {
                            try
                            {
                                await _weightRepo.InsertAsync(
                                    GlobalValue.SessionId,
                                    model.GrossWeight,
                                    model.RightWeight,
                                    model.LeftWeight,
                                    model.RightBattery,
                                    model.LeftBattery,
                                    model.RightIsCharging,
                                    model.leftIsCharging,
                                    model.RightOnline,
                                    model.LeftOnline,
                                    model.GrossNet,
                                    model.OverLoad,
                                    model.OutOfTolerance
                                );
                            }
                            catch { }
                        }, ThreadOption.BackgroundThread, true);
                }
            }
            catch
            {
                // DB가 없어도 앱은 계속 동작 (로그만 사용)
            }
        }

        protected override Window CreateShell()
        {
            var mainWindow = Container.Resolve<MainWindow>();
            mainWindow.WindowState = WindowState.Maximized;
            mainWindow.ResizeMode = ResizeMode.CanMinimize;
            mainWindow.ResizeMode = ResizeMode.CanResize;
            mainWindow.WindowStyle = WindowStyle.None;
            return Container.Resolve<MainWindow>();
        }

        protected override void RegisterTypes(IContainerRegistry containerRegistry)
        {
            var parser = new SystemJsonConfigParser();

            (IWeightModel weight, IDistanceModel distance,
                IRFIDModel rfid, IMainModel main, ILedBuzzertModel LedBuzzer,
                IDPSModel dpsmodel, INAVModel navmodel, IVisionCamModel visioncammodel,
                ILivoxModel livoxmodel, IDisplayModel displaymodel) = parser.LoadJsonfile();

            containerRegistry.RegisterSingleton<IWeightModel>(x => weight);
            containerRegistry.RegisterSingleton<IDistanceModel>(x => distance);
            containerRegistry.RegisterSingleton<IRFIDModel>(x => rfid);
            containerRegistry.RegisterSingleton<IMainModel>(x => main);
            containerRegistry.RegisterSingleton<ILedBuzzertModel>(x => LedBuzzer);
            containerRegistry.RegisterSingleton<IDPSModel>(x => dpsmodel);
            containerRegistry.RegisterSingleton<INAVModel>(x => navmodel);
            containerRegistry.RegisterSingleton<IVisionCamModel>(x => visioncammodel);
            containerRegistry.RegisterSingleton<ILivoxModel>(x => livoxmodel);
            containerRegistry.RegisterSingleton<IDisplayModel>(x => displaymodel);

            if (!containerRegistry.IsRegistered<IWeightModel>())
                containerRegistry.RegisterSingleton<IWeightModel, WeightConfigModel>();

            if (!containerRegistry.IsRegistered<IDistanceModel>())
                containerRegistry.RegisterSingleton<IDistanceModel, DistanceConfigModel>();

            if (!containerRegistry.IsRegistered<IVisionModel>())
                containerRegistry.RegisterSingleton<IVisionModel, VisionConfigModel>();

            if (!containerRegistry.IsRegistered<IRFIDModel>())
                containerRegistry.RegisterSingleton<IRFIDModel, RFIDConfigModel>();

            if (!containerRegistry.IsRegistered<IMainModel>())
                containerRegistry.RegisterSingleton<IMainModel, MainConfigModel>();

            if (!containerRegistry.IsRegistered<ILedBuzzertModel>())
                containerRegistry.RegisterSingleton<ILedBuzzertModel, Led_Buzzer_ConfigModel>();

            if (!containerRegistry.IsRegistered<IDPSModel>())
                containerRegistry.RegisterSingleton<IDPSModel, DPSConfigModel>();

            if (!containerRegistry.IsRegistered<INAVModel>())
                containerRegistry.RegisterSingleton<INAVModel, NAVConfigModel>();

            if (!containerRegistry.IsRegistered<IVisionCamModel>())
                containerRegistry.RegisterSingleton<IVisionCamModel, VisionCamConfigModel>();

            if (!containerRegistry.IsRegistered<ILivoxModel>())
                containerRegistry.RegisterSingleton<ILivoxModel, LIVOXConfigModel>();

            if (!containerRegistry.IsRegistered<IDisplayModel>())
                containerRegistry.RegisterSingleton<IDisplayModel, DisplayConfigModel>();

            // ILogger 등록
            containerRegistry.RegisterSingleton<ILoggerFactory, LoggerFactory>();
            containerRegistry.Register(typeof(ILogger<>), typeof(Logger<>));

            MainConfigModel mainobj = (MainConfigModel)main;

            if (mainobj.device_type == "fork_lift_v1")
            {
                containerRegistry.RegisterSingleton<IStatusService, StatusService_V1>();//현재 안씀 FH-920 RF수신기 2023.09.27 현재는 쓰지 않음
            }
            else if (mainobj.device_type == "pantos")// 판토스향
            {
                containerRegistry.RegisterSingleton<IStatusService, StatusService_Pantos>();//현재 지게차용  Apulse RF수신기
            }
            else if (mainobj.device_type == "calt")//칼트향
            {
                containerRegistry.RegisterSingleton<IStatusService, StatusService_CALT>();//현재 지게차용  Apulse RF수신기

            }
            else if (mainobj.device_type == "gate_checker")//창고방 Gate Sender 현재는 안씀
            {
                containerRegistry.RegisterSingleton<IStatusService, StatusService_GateChecker>();
            }
            else if (mainobj.device_type == "DPS")//DPS DPS 컨트롤 테스트때 사용
            {
                containerRegistry.RegisterSingleton<IStatusService, StatusService_DPS>();
            }
            else if (mainobj.device_type == "NXDPOC")//NXD POC 
            {
                containerRegistry.RegisterSingleton<IStatusService, StatusService_NXDPOC>();
            }
            else if (mainobj.device_type == "WIS_KINTEX")//국내전시회 3x3 선반용
            {
                containerRegistry.RegisterSingleton<IStatusService, StatusService_WIS_KINTEX>();
            }
            else if (mainobj.device_type == "CTR")//CTR POC
            {
                containerRegistry.RegisterSingleton<IStatusService, StatusService_CTR>();
            }
            else if (mainobj.device_type == "Singapore")//Singapore POC
            {
                containerRegistry.RegisterSingleton<IStatusService, StatusService_Singapore>();
            }
            else if (mainobj.device_type == "Clark")//Clark POC
            {
                containerRegistry.RegisterSingleton<IStatusService, StatusService_Clark>();
            }
            else if (mainobj.device_type == "Japan")//일본 시연
            {
                containerRegistry.RegisterSingleton<IStatusService, StatusService_Japan>();
            }
            else if (mainobj.device_type == "DHL_KOREA")//DHL KOREA 시연
            {
                containerRegistry.RegisterSingleton<IStatusService, StatusService_DHL_KOREA>();
            }
            else if (mainobj.device_type == "WATA")//내부 시연
            {
                containerRegistry.RegisterSingleton<IStatusService, StatusService_WATA>();
            }
            else if (mainobj.device_type == "Pantos_MTV")//LX판토스 시화 MTV 서비스 코드
            {
                containerRegistry.RegisterSingleton<IStatusService, StatusService_PantosMTV>();
            }
        }

        protected override void ConfigureModuleCatalog(IModuleCatalog moduleCatalog)
        {
            moduleCatalog.AddModule<LEDModule>();
            moduleCatalog.AddModule<DistanceModule>();
            moduleCatalog.AddModule<WEIGHTModule>();
            moduleCatalog.AddModule<BEModule>();
            moduleCatalog.AddModule<MainModule>();
            moduleCatalog.AddModule<UHF_RFIDModule>();
            moduleCatalog.AddModule<DISPLAYModule>();
            moduleCatalog.AddModule<NAVModule>();
            moduleCatalog.AddModule<CAMModule>();
            moduleCatalog.AddModule<LIVOXModule>();
        }
    }
}