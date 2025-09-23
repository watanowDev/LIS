using Prism.Ioc;
using Prism.Modularity;
using System;
using System.Windows;
using WATA.LIS.Core.Common;
using Prism.Events;
using WATA.LIS.Core.Events.WeightSensor;
using WATA.LIS.Core.Events.BackEnd;
using WATA.LIS.Core.Events.DistanceSensor;
using WATA.LIS.Core.Events.RFID;
using WATA.LIS.Core.Events.NAVSensor;
using System.Threading.Tasks;
using WATA.LIS.Core.Model.BackEnd;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Collections.Concurrent;
using Npgsql;
using WATA.LIS.Heartbeat.Services;
using Microsoft.Extensions.Logging;
using WATA.LIS.DB;
using WATA.LIS.Core.Interfaces;
using WATA.LIS.Core.Model.SystemConfig;
using WATA.LIS.Core.Parser;
using WATA.LIS.Core.Services;
using WATA.LIS.Core.Services.ServiceImpl;
using WATA.LIS.IF.BE;
using WATA.LIS.INDICATOR.DISPLAY;
using WATA.LIS.INDICATOR.LED;
using WATA.LIS.Main;
using WATA.LIS.SENSOR.Distance;
using WATA.LIS.SENSOR.NAV;
using WATA.LIS.SENSOR.UHF_RFID;
using WATA.LIS.SENSOR.WEIGHT;
using WATA.LIS.VISION.CAM;
using WATA.LIS.SENSOR.LIVOX;
using WATA.LIS.Views;
using WATA.LIS.Core.Model.RFID;
using WATA.LIS.Core.Events.System;

namespace WATA.LIS
{
    public partial class App
    {
        private HeartbeatService _heartbeatService;
        private SessionRepository _sessionRepo;
        private SystemStatusRepository _statusRepo;
        private System.Windows.Threading.DispatcherTimer _statusTimer;
        private WeightRepository _weightRepo;
        private DistanceRepository _distanceRepo;
        private RfidAggregateRepository _rfidAggRepo;
        private NavRepository _navRepo;
        private ActionEventRepository _actionEventRepo;
        private ActionPoseRepository _actionPoseRepo;
        private ActionSensorBundleRepository _actionBundleRepo;
        private IEventAggregator _eventAggregator;
        private volatile bool _networkOkFromBackend = false; // updated via BackEndStatusEvent

        // latest sensor snapshot cache (thread-safe via _snapshotLock)
        private readonly object _snapshotLock = new object();
        private long? _lastNavX, _lastNavY, _lastNavT;
        private string _lastZoneId, _lastZoneName, _lastProjectId, _lastMappingId, _lastMapId, _lastVehicleId;
        private int? _lastGross, _lastRight, _lastLeft;
        private int? _lastDistanceMm;
        private bool _lastDistanceConnected;
        private int? _lastRfidCount, _lastRfidAvgRssi;
        // latest extra weight fields
        private int _lastRightBattery, _lastLeftBattery;
        private bool _lastRightIsCharging, _lastLeftIsCharging, _lastRightOnline, _lastLeftOnline, _lastGrossNet, _lastOverLoad, _lastOutOfTolerance;
        private bool _weightSnapshotInitialized, _distanceSnapshotInitialized, _navSnapshotInitialized;

        // RFID aggregation state: first-seen set and 1Hz aggregator map
        private readonly HashSet<string> _rfidFirstSeen = new HashSet<string>();
        private readonly Dictionary<string, (int CountSum, long RssiSum)> _rfidAggMap = new Dictionary<string, (int, long)>();
        private string _rfidDominantEpc;
        private double _rfidDominantScore;
        private DateTimeOffset _rfidDominantTs;

        // SysAlarm tracking
        private readonly Dictionary<string, DateTimeOffset> _errorStart = new Dictionary<string, DateTimeOffset>();
        private string _lastErrorCodesSnapshot = "0000";

        // smoothing windows (short rolling average)
        private const int WeightWindowSize = 5;
        private const int RfidWindowSize = 5;
        private readonly Queue<int> _grossQueue = new Queue<int>();
        private readonly Queue<int> _rightQueue = new Queue<int>();
        private readonly Queue<int> _leftQueue = new Queue<int>();
        private int _grossSum = 0, _rightSum = 0, _leftSum = 0;
        private readonly Queue<int> _rfidCountQueue = new Queue<int>();
        private readonly Queue<int> _rfidRssiQueue = new Queue<int>();
        private int _rfidCountSum = 0, _rfidRssiSum = 0;
        // action post deduplication (avoid rapid duplicates from multiple events)
        private readonly ConcurrentDictionary<string, DateTimeOffset> _actionDedup = new ConcurrentDictionary<string, DateTimeOffset>();
        private TimeSpan _actionDedupWindow = TimeSpan.FromMilliseconds(300);

        // 필드 추가
        private AppLogRepository _appLogRepo;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            var connectionString = BuildConnectionStringFromConfigOrEnv();
            _ = InitializeDatabaseAsync(connectionString);
            var logger = Container.Resolve<Microsoft.Extensions.Logging.ILogger<HeartbeatService>>();
            _heartbeatService = new HeartbeatService(logger);
            _heartbeatService.Start();
            try
            {
                var mainCfg = Container.Resolve<IMainModel>() as MainConfigModel;
                int ms = 300;
                if (mainCfg != null && mainCfg.GetType() == typeof(MainConfigModel))
                {
                    var cfgMs = (mainCfg as MainConfigModel)?.action_dedup_ms ?? 300;
                    if (cfgMs > 0) ms = cfgMs;
                }
                ms = Math.Min(500, Math.Max(200, ms));
                _actionDedupWindow = TimeSpan.FromMilliseconds(ms);
                Tools.Log($"Action dedup window set to {ms} ms (using UTC timestamps)", Tools.ELogType.SystemLog);
            }
            catch { }
            _eventAggregator = Container.Resolve<IEventAggregator>();
            _eventAggregator
                .GetEvent<BackEndStatusEvent>()
                .Subscribe(status =>
                {
                    _networkOkFromBackend = status != -1;
                    GlobalValue.IS_ERROR.backend = _networkOkFromBackend;
                }, ThreadOption.BackgroundThread, true);

            _eventAggregator.GetEvent<ShutdownSnapshotEvent>()
                .Subscribe(async json => await HandleShutdownSnapshotAsync(json), ThreadOption.BackgroundThread, true);

            // DB 준비 후 복원 시도(이미 유사 코드가 있다면 그걸 사용)
            System.Windows.Application.Current?.Dispatcher.BeginInvoke(new Action(async () =>
            {
                try { await TryRestoreShutdownSnapshotAsync(); } catch { }
            }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }

        private static string SanitizeSearchPath(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "lis_core,public";
            var v = value.Trim().Replace("\"", string.Empty).Replace("'", string.Empty);
            v = v.Replace(";", ",").Replace("=", string.Empty);
            v = string.Join(",", v.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries));
            return string.IsNullOrWhiteSpace(v) ? "lis_core,public" : v;
        }

        private static string BuildConnectionStringFromConfigOrEnv()
        {
            var env = Environment.GetEnvironmentVariable("WATA_LIS_CONN");
            if (!string.IsNullOrWhiteSpace(env)) return env;
            try
            {
                var app = (App)Current;
                var main = app?.Container?.Resolve<IMainModel>() as MainConfigModel;
                string host = main?.db_host ?? "localhost";
                int port = main?.db_port ?? 5432;
                string database = !string.IsNullOrWhiteSpace(main?.db_database) ? main.db_database : "forkliftDB";
                string username = !string.IsNullOrWhiteSpace(main?.db_username) ? main.db_username : "postgres";
                string password = !string.IsNullOrWhiteSpace(main?.db_password) ? main.db_password : "wata2019";
                string searchPathRaw = !string.IsNullOrWhiteSpace(main?.db_search_path) ? main.db_search_path : "lis_core,public";
                string searchPath = SanitizeSearchPath(searchPathRaw);
                var cs = $"Host={host};Port={port};Database={database};Username={username};Password={password};SearchPath={searchPath};Pooling=true;Include Error Detail=true";
                return cs;
            }
            catch
            {
                return "Host=localhost;Port=5432;Database=forkliftDB;Username=postgres;Password=wata2019;SearchPath=lis_core,public;Pooling=true;Include Error Detail=true";
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _ = Task.Run(async () =>
            {
                try { if (_sessionRepo != null) await _sessionRepo.MarkEndAsync(DateTimeOffset.UtcNow, GlobalValue.SessionId); }
                catch { }
            });
            base.OnExit(e);
        }

        private async Task InitializeDatabaseAsync(string cs)
        {
            try
            {
                try
                {
                    var b = new NpgsqlConnectionStringBuilder(cs);
                    string host = b.Host;
                    int port = b.Port;
                    string db = b.Database;
                    string user = b.Username;
                    string sp = b.SearchPath;
                    Tools.Log($"DB connect attempt: {host}:{port}/{db} as {user}, SearchPath={sp}", Tools.ELogType.SystemLog);
                    await using (var c = new NpgsqlConnection(cs))
                    {
                        await c.OpenAsync();
                        await using var cmd = new NpgsqlCommand("select version(), current_setting('search_path', true)", c);
                        await using var r = await cmd.ExecuteReaderAsync();
                        if (await r.ReadAsync())
                        {
                            string ver = r.GetString(0);
                            string path = r.IsDBNull(1) ? null : r.GetString(1);
                            Tools.Log($"DB connected. Server={ver.Split(' ')[0]}, search_path={path}", Tools.ELogType.SystemLog);
                        }
                    }
                }
                catch (PostgresException pex)
                {
                    Tools.Log($"DB pre-check failed (PG): {pex.SqlState} {pex.MessageText}", Tools.ELogType.SystemLog);
                    throw;
                }
                catch (Exception dex)
                {
                    Tools.Log($"DB pre-check failed: {dex.Message}", Tools.ELogType.SystemLog);
                    throw;
                }

                var migrator = new MigrationService(cs);
                await migrator.EnsureSchemaAsync();

                _sessionRepo = new SessionRepository(cs);
                _statusRepo = new SystemStatusRepository(cs);
                await _statusRepo.EnsureTableAsync();
                var machine = Environment.MachineName;
                await _sessionRepo.UpsertStartAsync(DateTimeOffset.UtcNow, GlobalValue.SessionId, GlobalValue.SystemVersion, machine);

                _statusTimer = new System.Windows.Threading.DispatcherTimer();
                _statusTimer.Interval = TimeSpan.FromSeconds(1);
                _statusTimer.Tick += async (_, __) =>
                {
                    try
                    {
                        bool backendOk = GlobalValue.IS_ERROR.backend;
                        bool networkOk = _networkOkFromBackend || backendOk;
                        bool setAllReady = GlobalValue.IS_ERROR.camera &&
                                           GlobalValue.IS_ERROR.distance &&
                                           GlobalValue.IS_ERROR.rfid &&
                                           GlobalValue.IS_ERROR.backend;
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
                        await _statusRepo.InsertAsync(DateTimeOffset.UtcNow, backendOk, networkOk, setAllReady, errorCode, message, GlobalValue.SessionId);

                        // Throttled 1 Hz inserts
                        try
                        {
                            if (_weightRepo != null && _weightSnapshotInitialized)
                            {
                                int gross = _lastGross ?? 0;
                                int right = _lastRight ?? 0;
                                int left = _lastLeft ?? 0;
                                await _weightRepo.InsertAsync(
                                    DateTimeOffset.UtcNow,
                                    GlobalValue.SessionId,
                                    gross,
                                    right,
                                    left,
                                    _lastRightBattery,
                                    _lastLeftBattery,
                                    _lastRightIsCharging,
                                    _lastLeftIsCharging,
                                    _lastRightOnline,
                                    _lastLeftOnline,
                                    _lastGrossNet,
                                    _lastOverLoad,
                                    _lastOutOfTolerance
                                );
                            }
                        }
                        catch { }

                        try
                        {
                            if (_distanceRepo != null && _distanceSnapshotInitialized)
                            {
                                int dist = _lastDistanceMm ?? 0;
                                await _distanceRepo.InsertAsync(DateTimeOffset.UtcNow, GlobalValue.SessionId, dist, _lastDistanceConnected);
                            }
                        }
                        catch { }

                        try
                        {
                            if (_navRepo != null && _navSnapshotInitialized && _lastNavX.HasValue && _lastNavY.HasValue && _lastNavT.HasValue)
                            {
                                await _navRepo.InsertAsync(
                                    DateTimeOffset.UtcNow,
                                    GlobalValue.SessionId,
                                    _lastNavX.Value,
                                    _lastNavY.Value,
                                    _lastNavT.Value,
                                    _lastZoneId,
                                    _lastZoneName,
                                    _lastProjectId,
                                    _lastMappingId,
                                    _lastMapId,
                                    null,
                                    _lastVehicleId
                                );
                            }
                        }
                        catch { }

                        // Flush RFID aggregated readings (1 Hz)
                        try
                        {
                            if (_rfidAggRepo != null)
                            {
                                List<(string epc, int rssi, int count)> batch;
                                lock (_snapshotLock)
                                {
                                    batch = new List<(string, int, int)>(_rfidAggMap.Count);
                                    foreach (var kv in _rfidAggMap)
                                    {
                                        var epc = kv.Key;
                                        var (cnt, rssiSum) = kv.Value;
                                        if (cnt > 0)
                                        {
                                            int avgRssi = (int)Math.Round(rssiSum / (double)cnt);
                                            batch.Add((epc, avgRssi, cnt));
                                        }
                                    }
                                    _rfidAggMap.Clear();
                                }
                                foreach (var rec in batch)
                                {
                                    await _rfidAggRepo.Insert2chAsync(DateTimeOffset.UtcNow, GlobalValue.SessionId, rec.epc, rec.rssi, rec.count);
                                }
                            }
                        }
                        catch { }

                        // Detect SysAlarm changes and log as action events
                        try
                        {
                            var now = DateTimeOffset.UtcNow;
                            var curr = WATA.LIS.Core.Common.SysAlarm.CurrentErr ?? "0000";
                            var currCodes = new HashSet<string>(curr.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
                            currCodes.Remove("0000");
                            // raised codes
                            foreach (var code in currCodes)
                            {
                                if (!_errorStart.ContainsKey(code))
                                {
                                    _errorStart[code] = now;
                                    await LogAlarmActionAsync(now, code, state: "raise", durationMs: null);
                                }
                            }
                            // cleared codes
                            var toClear = new List<string>();
                            foreach (var kv in _errorStart)
                            {
                                if (!currCodes.Contains(kv.Key))
                                {
                                    var duration = (long)Math.Max(0, (now - kv.Value).TotalMilliseconds);
                                    await LogAlarmActionAsync(now, kv.Key, state: "clear", durationMs: duration);
                                    toClear.Add(kv.Key);
                                }
                            }
                            foreach (var code in toClear) _errorStart.Remove(code);
                            _lastErrorCodesSnapshot = curr;
                        }
                        catch { }
                    }
                    catch { }
                };
                _statusTimer.Start();

                _weightRepo = new WeightRepository(cs);
                await _weightRepo.EnsureTableAsync();
                if (_eventAggregator != null)
                {
                    _eventAggregator
                        .GetEvent<WeightSensorEvent>()
                        .Subscribe(model =>
                        {
                            try
                            {
                                lock (_snapshotLock)
                                {
                                    PushAndAverage(_grossQueue, ref _grossSum, model.GrossWeight, WeightWindowSize, out int grossAvg);
                                    PushAndAverage(_rightQueue, ref _rightSum, model.RightWeight, WeightWindowSize, out int rightAvg);
                                    PushAndAverage(_leftQueue, ref _leftSum, model.LeftWeight, WeightWindowSize, out int leftAvg);
                                    _lastGross = grossAvg;
                                    _lastRight = rightAvg;
                                    _lastLeft = leftAvg;
                                    _lastRightBattery = model.RightBattery;
                                    _lastLeftBattery = model.LeftBattery;
                                    _lastRightIsCharging = model.RightIsCharging;
                                    _lastLeftIsCharging = model.leftIsCharging;
                                    _lastRightOnline = model.RightOnline;
                                    _lastLeftOnline = model.LeftOnline;
                                    _lastGrossNet = model.GrossNet;
                                    _lastOverLoad = model.OverLoad;
                                    _lastOutOfTolerance = model.OutOfTolerance;
                                    _weightSnapshotInitialized = true;
                                }
                            }
                            catch { }
                        }, ThreadOption.BackgroundThread, true);
                }

                _distanceRepo = new DistanceRepository(cs);
                await _distanceRepo.EnsureTableAsync();
                _eventAggregator?.GetEvent<DistanceSensorEvent>()
                    .Subscribe(model =>
                    {
                        try
                        {
                            lock (_snapshotLock)
                            {
                                _lastDistanceMm = model.Distance_mm;
                                _lastDistanceConnected = model.connected;
                                _distanceSnapshotInitialized = true;
                            }
                        }
                        catch { }
                    }, ThreadOption.BackgroundThread, true);

                _rfidAggRepo = new RfidAggregateRepository(cs);
                await _rfidAggRepo.EnsureTableAsync();
                _eventAggregator?.GetEvent<Keonn2chEvent>()
                    .Subscribe(async list =>
                    {
                        try
                        {
                            if (list == null || list.Count == 0) return;

                            // Compute dominant by score = RSSI*1000 + ReadCount (simple heuristic)
                            Keonn2ch_Model top = null;
                            double topScore = double.MinValue;
                            foreach (var item in list)
                            {
                                if (item == null) continue;
                                double score = (item.RSSI * 1000.0) + Math.Max(1, item.READCNT);
                                if (score > topScore)
                                {
                                    topScore = score;
                                    top = item;
                                }
                            }

                            // If dominant changed, immediately log all EPCs from this batch once
                            bool loggedImmediateForBatch = false;
                            if (top != null)
                            {
                                bool isNewDominant = false;
                                lock (_snapshotLock)
                                {
                                    if (string.IsNullOrEmpty(_rfidDominantEpc) || !string.Equals(_rfidDominantEpc, top.EPC, StringComparison.Ordinal))
                                    {
                                        isNewDominant = true;
                                        _rfidDominantEpc = top.EPC;
                                        _rfidDominantScore = topScore;
                                        _rfidDominantTs = DateTimeOffset.UtcNow;
                                    }
                                }
                                if (isNewDominant)
                                {
                                    // Immediately log current batch (dominant + co-read EPCs)
                                    foreach (var item in list)
                                    {
                                        try { await _rfidAggRepo.Insert2chAsync(DateTimeOffset.UtcNow, GlobalValue.SessionId, item.EPC, item.RSSI, Math.Max(1, item.READCNT)); } catch { }
                                    }
                                    loggedImmediateForBatch = true;
                                }
                            }

                            int count = 0;
                            long sumRssi = 0;
                            foreach (var item in list)
                            {
                                if (item == null) continue;
                                var epc = item.EPC;
                                if (string.IsNullOrWhiteSpace(epc)) continue;

                                // First-seen immediate logging for header-matching EPCs
                                bool headerMatch = epc.StartsWith("DA", StringComparison.OrdinalIgnoreCase) || epc.StartsWith("DC", StringComparison.OrdinalIgnoreCase);
                                if (headerMatch && !loggedImmediateForBatch)
                                {
                                    bool firstSeen;
                                    lock (_snapshotLock)
                                    {
                                        firstSeen = _rfidFirstSeen.Add(epc);
                                    }
                                    if (firstSeen)
                                    {
                                        try { await _rfidAggRepo.Insert2chAsync(DateTimeOffset.UtcNow, GlobalValue.SessionId, epc, item.RSSI, Math.Max(1, item.READCNT)); } catch { }
                                    }
                                }

                                // Aggregate for 1 Hz flush
                                lock (_snapshotLock)
                                {
                                    if (_rfidAggMap.TryGetValue(epc, out var agg))
                                        _rfidAggMap[epc] = (agg.CountSum + Math.Max(1, item.READCNT), agg.RssiSum + item.RSSI);
                                    else
                                        _rfidAggMap[epc] = (Math.Max(1, item.READCNT), item.RSSI);
                                }

                                count++;
                                sumRssi += item.RSSI;
                            }

                            // update indicator snapshot averages
                            try
                            {
                                int rfidCnt = count;
                                int avg = rfidCnt > 0 ? (int)Math.Round(sumRssi / (double)rfidCnt) : 0;
                                lock (_snapshotLock)
                                {
                                    PushAndAverage(_rfidCountQueue, ref _rfidCountSum, rfidCnt, RfidWindowSize, out int rfidCntAvg);
                                    PushAndAverage(_rfidRssiQueue, ref _rfidRssiSum, rfidCnt > 0 ? avg : 0, RfidWindowSize, out int rssiAvg);
                                    _lastRfidCount = rfidCntAvg;
                                    _lastRfidAvgRssi = (rfidCntAvg > 0 && rssiAvg != 0) ? rssiAvg : (int?)null;
                                }
                            }
                            catch { }
                        }
                        catch { }
                    }, ThreadOption.BackgroundThread, true);

                _navRepo = new NavRepository(cs);
                await _navRepo.EnsureTableAsync();
                _actionPoseRepo = new ActionPoseRepository(cs);
                await _actionPoseRepo.EnsureTableAsync();
                _actionBundleRepo = new ActionSensorBundleRepository(cs);
                await _actionBundleRepo.EnsureTableAsync();
                _eventAggregator?.GetEvent<NAVSensorEvent>()
                    .Subscribe(nav =>
                    {
                        try
                        {
                            lock (_snapshotLock)
                            {
                                _lastNavX = nav.naviX;
                                _lastNavY = nav.naviY;
                                _lastNavT = nav.naviT;
                                _lastZoneId = nav.zoneId;
                                _lastZoneName = nav.zoneName;
                                _lastProjectId = nav.projectId;
                                _lastMappingId = nav.mappingId;
                                _lastMapId = nav.mapId;
                                _lastVehicleId = nav.vehicleId;
                                _navSnapshotInitialized = true;
                            }
                        }
                        catch { }
                    }, ThreadOption.BackgroundThread, true);

                _actionEventRepo = new ActionEventRepository(cs);
                await _actionEventRepo.EnsureTableAsync();
                void HandleActionPost(RestClientPostModel post)
                {
                    try
                    {
                        if (post == null) return;
                        if (post.type != WATA.LIS.Core.Common.eMessageType.BackEndAction && post.type != WATA.LIS.Core.Common.eMessageType.BackEndContainer) return;
                        var now = DateTimeOffset.UtcNow;
                        var dedupKey = $"{post.url}|{post.body}";
                        if (_actionDedup.TryGetValue(dedupKey, out var last) && (now - last) < _actionDedupWindow)
                        {
                            return;
                        }
                        _actionDedup[dedupKey] = now;
                        var eventTime = now;
                        string action = null;
                        string workLocationId = null;
                        string loadId = null;
                        string epc = null;
                        string cepc = null;
                        try
                        {
                            if (!string.IsNullOrWhiteSpace(post.body))
                            {
                                var j = JObject.Parse(post.body);
                                action = (string)j["action"] ?? (string)j["type"];
                                workLocationId = (string)j["workLocationId"] ?? (string)j["work_location_id"];
                                loadId = (string)j["loadId"] ?? (string)j["load_id"];
                                epc = (string)j["epc"];
                                cepc = (string)j["cepc"];
                                if (string.IsNullOrEmpty(action) && post.type == WATA.LIS.Core.Common.eMessageType.BackEndContainer)
                                    action = "container_gate";
                            }
                        }
                        catch { }
                        _ = _actionEventRepo.InsertAsync(
                            eventTime,
                            GlobalValue.SessionId,
                            action,
                            workLocationId,
                            loadId,
                            epc,
                            cepc,
                            post.url,
                            post.body,
                            "RestClientPost"
                        );
                        long x = 0, y = 0, t = 0;
                        string zoneId = null, zoneName = null, projectId = null, mappingId = null, mapId = null, vehicleId = null;
                        int? gross = null, right = null, left = null, dist = null, rfidCnt = null, rfidAvg = null;
                        lock (_snapshotLock)
                        {
                            if (_lastNavX.HasValue) x = _lastNavX.Value;
                            if (_lastNavY.HasValue) y = _lastNavY.Value;
                            if (_lastNavT.HasValue) t = _lastNavT.Value;
                            zoneId = _lastZoneId;
                            zoneName = _lastZoneName;
                            projectId = _lastProjectId;
                            mappingId = _lastMappingId;
                            mapId = _lastMapId;
                            vehicleId = _lastVehicleId;
                            gross = _lastGross;
                            right = _lastRight;
                            left = _lastLeft;
                            dist = _lastDistanceMm;
                            rfidCnt = _lastRfidCount;
                            rfidAvg = _lastRfidAvgRssi;
                        }
                        _ = _actionPoseRepo.InsertAsync(
                            eventTime,
                            GlobalValue.SessionId,
                            x, y, t,
                            zoneId, zoneName, projectId, mappingId, mapId, vehicleId,
                            action
                        );
                        _ = _actionBundleRepo.InsertAsync(
                            eventTime,
                            GlobalValue.SessionId,
                            gross, right, left, dist, rfidCnt, rfidAvg
                        );
                    }
                    catch { }
                }
                _eventAggregator?.GetEvent<RestClientPostEvent>().Subscribe(HandleActionPost, ThreadOption.BackgroundThread, true);
                _eventAggregator?.GetEvent<RestClientPostEvent_dev>().Subscribe(HandleActionPost, ThreadOption.BackgroundThread, true);
            }
            catch (PostgresException ex)
            {
                try { Tools.Log($"DB init failed (PG): {ex.SqlState} {ex.MessageText}", Tools.ELogType.SystemLog); } catch { }
            }
            catch (Exception ex)
            {
                try { Tools.Log($"DB init failed: {ex.Message}", Tools.ELogType.SystemLog); } catch { }
            }
        }

        private async Task LogAlarmActionAsync(DateTimeOffset eventTime, string code, string state, long? durationMs)
        {
            try
            {
                var payload = new JObject
                {
                    ["code"] = code,
                    ["state"] = state,
                    ["durationMs"] = durationMs != null ? new JValue(durationMs.Value) : null,
                    ["errors"] = WATA.LIS.Core.Common.SysAlarm.CurrentErr
                };
                string body = payload.ToString();
                await _actionEventRepo.InsertAsync(
                    eventTime,
                    GlobalValue.SessionId,
                    action: state == "raise" ? "alarm_raise" : "alarm_clear",
                    workLocationId: null,
                    loadId: null,
                    epc: null,
                    cepc: null,
                    requestUrl: "SysAlarm",
                    requestBodyJson: body,
                    source: "SysAlarm"
                );
                long x = 0, y = 0, t = 0;
                string zoneId = null, zoneName = null, projectId = null, mappingId = null, mapId = null, vehicleId = null;
                int? gross = null, right = null, left = null, dist = null, rfidCnt = null, rfidAvg = null;
                lock (_snapshotLock)
                {
                    if (_lastNavX.HasValue) x = _lastNavX.Value;
                    if (_lastNavY.HasValue) y = _lastNavY.Value;
                    if (_lastNavT.HasValue) t = _lastNavT.Value;
                    zoneId = _lastZoneId;
                    zoneName = _lastZoneName;
                    projectId = _lastProjectId;
                    mappingId = _lastMappingId;
                    mapId = _lastMapId;
                    vehicleId = _lastVehicleId;
                    gross = _lastGross;
                    right = _lastRight;
                    left = _lastLeft;
                    dist = _lastDistanceMm;
                    rfidCnt = _lastRfidCount;
                    rfidAvg = _lastRfidAvgRssi;
                }
                await _actionPoseRepo.InsertAsync(eventTime, GlobalValue.SessionId, x, y, t, zoneId, zoneName, projectId, mappingId, mapId, vehicleId, state);
                await _actionBundleRepo.InsertAsync(eventTime, GlobalValue.SessionId, gross, right, left, dist, rfidCnt, rfidAvg);
            }
            catch { }
        }

        protected override Window CreateShell()
        {
            var mainWindow = Container.Resolve<MainWindow>();
            // Respect taskbar: use bordered window (not None) and normal state; size to WorkArea in MainWindow_Loaded
            mainWindow.WindowStyle = WindowStyle.SingleBorderWindow;
            mainWindow.ResizeMode = System.Windows.ResizeMode.CanResize;
            mainWindow.Topmost = false;
            return mainWindow;
        }

        protected override void RegisterTypes(IContainerRegistry containerRegistry)
        {
            var parser = new SystemJsonConfigParser();

            (IWeightModel weight, IDistanceModel distance,
                IRFIDModel rfid, IMainModel main, ILedBuzzertModel LedBuzzer,
                INAVModel navmodel, IVisionCamModel visioncammodel,
                ILivoxModel livoxmodel, IDisplayModel displaymodel) = parser.LoadJsonfile();

            containerRegistry.RegisterSingleton<IWeightModel>(x => weight);
            containerRegistry.RegisterSingleton<IDistanceModel>(x => distance);
            containerRegistry.RegisterSingleton<IRFIDModel>(x => rfid);
            containerRegistry.RegisterSingleton<IMainModel>(x => main);
            containerRegistry.RegisterSingleton<ILedBuzzertModel>(x => LedBuzzer);
            containerRegistry.RegisterSingleton<INAVModel>(x => navmodel);
            containerRegistry.RegisterSingleton<IVisionCamModel>(x => visioncammodel);
            containerRegistry.RegisterSingleton<ILivoxModel>(x => livoxmodel);
            containerRegistry.RegisterSingleton<IDisplayModel>(x => displaymodel);

            if (!containerRegistry.IsRegistered<IWeightModel>())
                containerRegistry.RegisterSingleton<IWeightModel, WeightConfigModel>();

            if (!containerRegistry.IsRegistered<IDistanceModel>())
                containerRegistry.RegisterSingleton<IDistanceModel, DistanceConfigModel>();

            if (!containerRegistry.IsRegistered<IRFIDModel>())
                containerRegistry.RegisterSingleton<IRFIDModel, RFIDConfigModel>();

            if (!containerRegistry.IsRegistered<IMainModel>())
                containerRegistry.RegisterSingleton<IMainModel, MainConfigModel>();

            if (!containerRegistry.IsRegistered<ILedBuzzertModel>())
                containerRegistry.RegisterSingleton<ILedBuzzertModel, Led_Buzzer_ConfigModel>();

            if (!containerRegistry.IsRegistered<INAVModel>())
                containerRegistry.RegisterSingleton<INAVModel, NAVConfigModel>();

            if (!containerRegistry.IsRegistered<IVisionCamModel>())
                containerRegistry.RegisterSingleton<IVisionCamModel, VisionCamConfigModel>();

            if (!containerRegistry.IsRegistered<ILivoxModel>())
                containerRegistry.RegisterSingleton<ILivoxModel, LIVOXConfigModel>();

            if (!containerRegistry.IsRegistered<IDisplayModel>())
                containerRegistry.RegisterSingleton<IDisplayModel, DisplayConfigModel>();

            containerRegistry.RegisterSingleton<ILoggerFactory, LoggerFactory>();
            containerRegistry.Register(typeof(ILogger<>), typeof(Logger<>));

            MainConfigModel mainobj = (MainConfigModel)main;

            if (mainobj.device_type == "WATA")
            {
                containerRegistry.RegisterSingleton<IStatusService, StatusService_WATA>();
            }
            else if (mainobj.device_type == "Pantos_MTV")
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

        private static void PushAndAverage(Queue<int> q, ref int sum, int value, int windowSize, out int avg)
        {
            sum += value;
            q.Enqueue(value);
            if (q.Count > windowSize)
            {
                sum -= q.Dequeue();
            }
            int denom = q.Count;
            avg = denom > 0 ? (int)Math.Round(sum / (double)denom) : value;
        }

        // 저장 핸들러
        private async Task HandleShutdownSnapshotAsync(string json)
        {
            try
            {
                if (_appLogRepo == null)
                {
                    var cs = BuildConnectionStringFromConfigOrEnv();
                    _appLogRepo = new AppLogRepository(cs);
                    await _appLogRepo.EnsureTableAsync();
                }

                await _appLogRepo.InsertAsync(
                    createdAt: DateTimeOffset.UtcNow,
                    sessionId: GlobalValue.SessionId,
                    category: "ShutdownSnapshot",
                    message: "shutdown_snapshot",
                    source: "App.xaml.cs",
                    lineNumber: null,
                    threadId: System.Threading.Thread.CurrentThread.ManagedThreadId,
                    machineName: Environment.MachineName,
                    vehicleId: Container.Resolve<IMainModel>() is MainConfigModel m ? m.vehicleId : null,
                    workLocationId: (Container.Resolve<IMainModel>() as MainConfigModel)?.workLocationId,
                    projectId: (Container.Resolve<IMainModel>() as MainConfigModel)?.projectId,
                    mappingId: (Container.Resolve<IMainModel>() as MainConfigModel)?.mappingId,
                    mapId: (Container.Resolve<IMainModel>() as MainConfigModel)?.mapId,
                    level: "INFO",
                    correlationId: null,
                    contextJson: json,
                    tags: new[] { "snapshot", "shutdown" }
                );
                Tools.Log("[SNAPSHOT] saved to app_logs", Tools.ELogType.SystemLog);
            }
            catch (Exception ex)
            {
                Tools.Log($"[SNAPSHOT] save failed: {ex.Message}", Tools.ELogType.SystemLog);
            }
            finally
            {
                // UI 스레드에서 안전 종료
                var app = System.Windows.Application.Current;
                if (app != null)
                {
                    if (app.Dispatcher.CheckAccess()) app.Shutdown();
                    else app.Dispatcher.BeginInvoke(new Action(() => app.Shutdown()));
                }
            }
        }

        // 최근 30초 이내 스냅샷 복원
        private async Task TryRestoreShutdownSnapshotAsync()
        {
            try
            {
                if (_appLogRepo == null)
                {
                    var cs = BuildConnectionStringFromConfigOrEnv();
                    _appLogRepo = new AppLogRepository(cs);
                    await _appLogRepo.EnsureTableAsync();
                }

                var row = await _appLogRepo.GetLastByCategoryAsync("ShutdownSnapshot");
                if (row == null || string.IsNullOrWhiteSpace(row.ContextJson)) return;

                var age = DateTimeOffset.UtcNow - row.CreatedAt;
                if (age > TimeSpan.FromSeconds(30)) return;

                // StatusService_WATA가 복원하도록 방송
                _eventAggregator.GetEvent<RestorePickupStateEvent>().Publish(row.ContextJson);
                Tools.Log($"[SNAPSHOT] broadcast restore (age={age.TotalSeconds:F1}s)", Tools.ELogType.SystemLog);
            }
            catch (Exception ex)
            {
                Tools.Log($"[SNAPSHOT] restore check failed: {ex.Message}", Tools.ELogType.SystemLog);
            }
        }
    }
}