using MQTTnet;
using Newtonsoft.Json;
using Prism.Events;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WATA.LIS.Core.Common;
using WATA.LIS.Core.Events.NAVSensor;
using WATA.LIS.Core.Model.NAV;

namespace WATA.LIS.SENSOR.NAV.NAV
{
    public class SeyondSensor
    {
        private readonly IEventAggregator _eventAggregator;
        private MQTTnet.Server.MqttServer _mqttServer; // MQTT 브로커(서버)
        private CancellationTokenSource _cancellationTokenSource;

        // CSV 저장을 위한 데이터 버퍼
        private readonly List<PubSeyondModel> _seyondDataBuffer = new List<PubSeyondModel>();
        private readonly List<NAVSensorDataModel> _navDataBuffer = new List<NAVSensorDataModel>();
        private readonly object _bufferLock = new object();

        // 통합 센서 모델 (target_map + nav_map 데이터 통합)
        private PubSeyondModel _currentModel = new PubSeyondModel();
        private readonly object _modelLock = new object();

        // MQTT 설정
        private const int MqttBrokerPort = 1883; // MQTT 브로커 포트 (Windows PC에서 실행)

        private const int MaxBufferSize = 500;
        private string _csvExportDirectory;
        private bool _autoExportEnabled = true;

        public class NAVSensorDataModel
        {
            public DateTime TimeStamp { get; set; }
            public NAVSensorModel NavData { get; set; }
        }

        public SeyondSensor(IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;
            _eventAggregator.GetEvent<PubSeyondEvent>().Subscribe(OnPubSeyondEvent, ThreadOption.BackgroundThread, true);
            _eventAggregator.GetEvent<NAVSensorEvent>().Subscribe(OnNAVSensorEvent, ThreadOption.BackgroundThread, true);
        }

        private void OnPubSeyondEvent(PubSeyondModel model)
        {
            // 수신받은 Seyond 데이터 처리 로직
        }

        private void OnNAVSensorEvent(NAVSensorModel model)
        {
            lock (_bufferLock)
            {
                var navDataWithTime = new NAVSensorDataModel
                {
                    TimeStamp = DateTime.Now,
                    NavData = model
                };

                _navDataBuffer.Add(navDataWithTime);

                //Tools.Log($"[SeyondSensor] NAV data buffered - X:{model.naviX}, Y:{model.naviY}, T:{model.naviT}, Time:{navDataWithTime.TimeStamp:yyyy-MM-dd HH:mm:ss.fff}",
                //Tools.ELogType.SystemLog);

                CheckAndAutoExport();
            }
        }

        public void Init(string csvExportDirectory = null)
        {
            _csvExportDirectory = string.IsNullOrEmpty(csvExportDirectory)
                ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SensorData")
                : csvExportDirectory;

            if (!Directory.Exists(_csvExportDirectory))
            {
                Directory.CreateDirectory(_csvExportDirectory);
            }

            Tools.Log($"[SeyondSensor] CSV export directory: {_csvExportDirectory}", Tools.ELogType.SystemLog);

            InitMqttServer();
        }

        private async void InitMqttServer()
        {
            try
            {
                var mqttFactory = new MqttFactory();

                // MQTT 브로커(서버) 생성 및 설정
                var optionsBuilder = new MQTTnet.Server.MqttServerOptionsBuilder()
                    .WithDefaultEndpoint()
                    .WithDefaultEndpointPort(MqttBrokerPort);

                _mqttServer = mqttFactory.CreateMqttServer(optionsBuilder.Build());

                // 메시지 수신 이벤트 핸들러 등록 (모든 토픽 수신)
                _mqttServer.InterceptingPublishAsync += OnMqttMessageReceived;

                // 클라이언트 연결 이벤트 핸들러
                _mqttServer.ClientConnectedAsync += e =>
                {
                    Tools.Log($"[SeyondSensor] MQTT client connected: {e.ClientId}", Tools.ELogType.SystemLog);
                    return Task.CompletedTask;
                };

                // 클라이언트 연결 끊김 이벤트 핸들러
                _mqttServer.ClientDisconnectedAsync += e =>
                {
                    Tools.Log($"[SeyondSensor] MQTT client disconnected: {e.ClientId} (Reason: {e.DisconnectType})", Tools.ELogType.SystemLog);
                    return Task.CompletedTask;
                };

                // MQTT 브로커 시작
                await _mqttServer.StartAsync();

                Tools.Log($"[SeyondSensor] MQTT Broker started on port {MqttBrokerPort}", Tools.ELogType.SystemLog);
                Tools.Log($"[SeyondSensor] Listening for all topics from any client...", Tools.ELogType.SystemLog);
            }
            catch (Exception ex)
            {
                Tools.Log($"[SeyondSensor] Failed to initialize MQTT broker: {ex.Message}", Tools.ELogType.SystemLog);
                Tools.Log($"[SeyondSensor] Stack trace: {ex.StackTrace}", Tools.ELogType.SystemLog);
            }
        }

        /// <summary>
        /// MQTT 메시지 수신 핸들러 (모든 토픽 수신)
        /// </summary>
        private Task OnMqttMessageReceived(MQTTnet.Server.InterceptingPublishEventArgs e)
        {
            try
            {
                string topic = e.ApplicationMessage.Topic;
                byte[] payloadBytes = e.ApplicationMessage.Payload;
                string payload = Encoding.UTF8.GetString(payloadBytes);

                //Tools.Log($"[SeyondSensor] MQTT message received - Topic: {topic}, Payload: {payload}", Tools.ELogType.ActionLog);

                // JSON 파싱 및 처리 (토픽 정보 전달)
                ProcessMessage(topic, payload);
            }
            catch (Exception ex)
            {
                //Tools.Log($"[SeyondSensor] Error processing MQTT message: {ex.Message}", Tools.ELogType.SystemLog);
            }

            return Task.CompletedTask;
        }

        private void ProcessMessage(string topic, string message)
        {
            try
            {
                // JSON 역직렬화
                var jsonModel = JsonConvert.DeserializeObject<dynamic>(message);

                if (jsonModel == null)
                {
                    Tools.Log($"[SeyondSensor] Failed to parse JSON: {message}", Tools.ELogType.SystemLog);
                    return;
                }

                lock (_modelLock)
                {
                    // target_map 토픽 처리 (X, Y, Z, Role, Pitch, Yaw)
                    if (topic == "target_map")
                    {
                        _currentModel.X = (double)jsonModel.X;
                        _currentModel.Y = (double)jsonModel.Y;
                        _currentModel.Z = (double)jsonModel.Z;
                        _currentModel.Role = (double)jsonModel.Role;
                        _currentModel.Pitch = (double)jsonModel.Pitch;
                        _currentModel.Yaw = (double)jsonModel.Yaw;
                        _currentModel.TimeStamp = ConvertUnixTimestampToDateTime((double)jsonModel.TimeStamp);

                        //Tools.Log($"[SeyondSensor] target_map updated - X:{_currentModel.X}, Y:{_currentModel.Y}, Z:{_currentModel.Z}, Roll:{_currentModel.Role}, Pitch:{_currentModel.Pitch}, Yaw:{_currentModel.Yaw}",
                        //    Tools.ELogType.ActionLog);
                    }
                    // nav_map 토픽 처리 (PosX, PosY, PosH)
                    else if (topic == "nav_map")
                    {
                        _currentModel.PosX = (long)jsonModel.NAV_X;
                        _currentModel.PosY = (long)jsonModel.NAV_Y;
                        _currentModel.PosH = (int)jsonModel.NAV_H;

                        //Tools.Log($"[SeyondSensor] nav_map updated - PosX:{_currentModel.PosX}, PosY:{_currentModel.PosY}, PosH:{_currentModel.PosH}",
                        //    Tools.ELogType.ActionLog);
                    }
                    else
                    {
                        Tools.Log($"[SeyondSensor] Unknown topic: {topic}", Tools.ELogType.SystemLog);
                        return;
                    }

                    // 통합 모델 복사 후 발행
                    var publishModel = new PubSeyondModel
                    {
                        X = _currentModel.X,
                        Y = _currentModel.Y,
                        Z = _currentModel.Z,
                        Role = _currentModel.Role,
                        Pitch = _currentModel.Pitch,
                        Yaw = _currentModel.Yaw,
                        PosX = _currentModel.PosX,
                        PosY = _currentModel.PosY,
                        PosH = _currentModel.PosH,
                        TimeStamp = _currentModel.TimeStamp
                    };

                    // 버퍼에 추가
                    lock (_bufferLock)
                    {
                        _seyondDataBuffer.Add(publishModel);
                        CheckAndAutoExport();
                    }

                    // 통합 이벤트 발행
                    _eventAggregator.GetEvent<PubSeyondEvent>().Publish(publishModel);
                }
            }
            catch (JsonException jsonEx)
            {
                Tools.Log($"[SeyondSensor] JSON parsing error: {jsonEx.Message} | Message: {message}", Tools.ELogType.SystemLog);
            }
            catch (Exception ex)
            {
                Tools.Log($"[SeyondSensor] Failed to process message: {ex.Message}", Tools.ELogType.SystemLog);
            }
        }

        /// <summary>
        /// Unix 타임스탬프(초)를 DateTime으로 변환
        /// </summary>
        private DateTime ConvertUnixTimestampToDateTime(double unixTimeStamp)
        {
            // Unix epoch: 1970-01-01 00:00:00 UTC
            DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return epoch.AddSeconds(unixTimeStamp).ToLocalTime();
        }

        private void CheckAndAutoExport()
        {
            if (!_autoExportEnabled)
                return;

            if (_navDataBuffer.Count >= MaxBufferSize || _seyondDataBuffer.Count >= MaxBufferSize)
            {
                Tools.Log($"[SeyondSensor] Buffer full - Auto exporting CSV (NAV:{_navDataBuffer.Count}, Seyond:{_seyondDataBuffer.Count})",
                    Tools.ELogType.SystemLog);
                Task.Run(() => AutoExportAndClear());
            }
        }

        private void AutoExportAndClear()
        {
            try
            {
                if (ExportPairedDataToCSV(_csvExportDirectory))
                {
                    lock (_bufferLock)
                    {
                        int navCount = _navDataBuffer.Count;
                        int seyondCount = _seyondDataBuffer.Count;
                        _navDataBuffer.Clear();
                        _seyondDataBuffer.Clear();
                        Tools.Log($"[SeyondSensor] Auto export completed - Buffers cleared (NAV:{navCount}, Seyond:{seyondCount})",
                            Tools.ELogType.ActionLog);
                    }
                }
            }
            catch (Exception ex)
            {
                Tools.Log($"[SeyondSensor] Auto export failed: {ex.Message}", Tools.ELogType.SystemLog);
            }
        }

        public bool ExportPairedDataToCSV(string directoryPath = null)
        {
            try
            {
                lock (_bufferLock)
                {
                    if (_seyondDataBuffer.Count == 0 && _navDataBuffer.Count == 0)
                    {
                        Tools.Log("[SeyondSensor] No data to export (both buffers empty)", Tools.ELogType.SystemLog);
                        return false;
                    }

                    if (string.IsNullOrEmpty(directoryPath))
                    {
                        directoryPath = _csvExportDirectory ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SensorData");
                    }

                    if (!Directory.Exists(directoryPath))
                    {
                        Directory.CreateDirectory(directoryPath);
                    }

                    string fileName = $"NAV_Seyond_Paired_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                    string filePath = Path.Combine(directoryPath, fileName);

                    using (var writer = new StreamWriter(filePath, false, Encoding.UTF8))
                    {
                        writer.WriteLine("Index,NAV_TimeStamp,NAV_X,NAV_Y,NAV_T," +
                                       "Seyond_TimeStamp,Seyond_X,Seyond_Y,Syond_Z,Seyond_Role,Seyond_Pitch,Seyond_Yaw");

                        int maxCount = Math.Max(_navDataBuffer.Count, _seyondDataBuffer.Count);

                        for (int i = 0; i < maxCount; i++)
                        {
                            var navPart = i < _navDataBuffer.Count ? _navDataBuffer[i] : null;
                            var seyondPart = i < _seyondDataBuffer.Count ? _seyondDataBuffer[i] : null;

                            string navData = navPart != null
                                ? $"{navPart.TimeStamp:yyyy-MM-dd HH:mm:ss.fff},{navPart.NavData.naviX},{navPart.NavData.naviY},{navPart.NavData.naviT}"
                                : ",,,";

                            string seyondData = seyondPart != null
                                ? $"{seyondPart.TimeStamp:yyyy-MM-dd HH:mm:ss.fff},{seyondPart.X},{seyondPart.Y},{seyondPart.Z}," +
                                  $"{seyondPart.Role},{seyondPart.Pitch},{seyondPart.Yaw}"
                                : ",,,,,,";

                            writer.WriteLine($"{i},{navData},{seyondData}");
                        }
                    }

                    Tools.Log($"[SeyondSensor] Paired CSV export successful - NAV:{_navDataBuffer.Count}, Seyond:{_seyondDataBuffer.Count} records saved to {filePath}",
                        Tools.ELogType.SystemLog);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Tools.Log($"[SeyondSensor] Paired CSV export failed: {ex.Message}", Tools.ELogType.SystemLog);
                return false;
            }
        }

        public void SetAutoExportEnabled(bool enabled)
        {
            _autoExportEnabled = enabled;
            Tools.Log($"[SeyondSensor] Auto export {(enabled ? "enabled" : "disabled")}", Tools.ELogType.SystemLog);
        }

        public (int NavCount, int SeyondCount) GetBufferedDataCount()
        {
            lock (_bufferLock)
            {
                return (_navDataBuffer.Count, _seyondDataBuffer.Count);
            }
        }

        public void ClearBuffer()
        {
            lock (_bufferLock)
            {
                int navCount = _navDataBuffer.Count;
                int seyondCount = _seyondDataBuffer.Count;
                _navDataBuffer.Clear();
                _seyondDataBuffer.Clear();
                Tools.Log($"[SeyondSensor] Buffers cleared - NAV:{navCount}, Seyond:{seyondCount} records removed", Tools.ELogType.SystemLog);
            }
        }

        public async void Stop()
        {
            try
            {
                lock (_bufferLock)
                {
                    if (_navDataBuffer.Count > 0 || _seyondDataBuffer.Count > 0)
                    {
                        Tools.Log($"[SeyondSensor] Exporting remaining data before stop (NAV:{_navDataBuffer.Count}, Seyond:{_seyondDataBuffer.Count})",
                            Tools.ELogType.SystemLog);
                        ExportPairedDataToCSV(_csvExportDirectory);
                        _navDataBuffer.Clear();
                        _seyondDataBuffer.Clear();
                    }
                }

                // MQTT 브로커 종료
                if (_mqttServer != null)
                {
                    var stopOptions = new MQTTnet.Server.MqttServerStopOptions();
                    await _mqttServer.StopAsync(stopOptions);
                    _mqttServer.Dispose();
                    Tools.Log("[SeyondSensor] MQTT broker stopped", Tools.ELogType.SystemLog);
                }
            }
            catch (Exception ex)
            {
                Tools.Log($"[SeyondSensor] Error stopping MQTT broker: {ex.Message}", Tools.ELogType.SystemLog);
            }
        }
    }
}