using NetMQ.Sockets;
using Prism.Events;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using WATA.LIS.Core.Common;
using WATA.LIS.Core.Events.LIVOX;
using WATA.LIS.Core.Interfaces;
using WATA.LIS.Core.Model.LIVOX;
using WATA.LIS.Core.Model.SystemConfig;
using NetMQ;
using Newtonsoft.Json.Linq;
using static WATA.LIS.Core.Common.Tools;

namespace WATA.LIS.SENSOR.LIVOX.MQTT
{
    public class Hummingbird
    {
        private readonly IEventAggregator _eventAggregator;
        private readonly ILivoxModel _livoxmodel;

        LIVOXConfigModel livoxConfig;

        private DispatcherTimer mCheckConnTimer;
        private bool mConnected = false;

        public SubscriberSocket _subscriberSocket;
        private CancellationTokenSource _cancellationTokenSource;

        private readonly object _sync = new object();
        private volatile bool _reconnecting = false;

        // ⭐ 서버 역할: 0.0.0.0으로 Bind (모든 네트워크 인터페이스에서 수신)
        private const string SubEndpoint = "tcp://0.0.0.0:5003";
        private const int MaxErrorsBeforeReconnect = 3;

        private int _consecutiveErrors = 0;
        private DateTime _lastRxUtc = DateTime.MinValue;

        public Hummingbird(IEventAggregator eventAggregator, ILivoxModel livoxmodel)
        {
            _eventAggregator = eventAggregator;
            _livoxmodel = livoxmodel;
            livoxConfig = (LIVOXConfigModel)_livoxmodel;
        }

        public void Init()
        {
            InitHummingbird();
            StartHealthCheck();
        }

        private void InitHummingbird()
        {
            lock (_sync)
            {
                try
                {
                    CloseSocketsNoThrow();

                    _subscriberSocket = new SubscriberSocket();
                    _subscriberSocket.Options.ReceiveHighWatermark = 1000;

                    // 서버 역할
                    _subscriberSocket.Bind(SubEndpoint);

                    // 모든 토픽 구독
                    _subscriberSocket.SubscribeToAnyTopic();

                    Tools.Log($"[Hummingbird] Bound (Server) at {SubEndpoint} (subscribed to ANY topic)", Tools.ELogType.SystemLog);

                    _cancellationTokenSource = new CancellationTokenSource();
                    var token = _cancellationTokenSource.Token;

                    // 백그라운드 Task로 메시지 수신 시작
                    Task.Factory.StartNew(() => ReceiveMessages(token), token, TaskCreationOptions.LongRunning, TaskScheduler.Default);

                    _lastRxUtc = DateTime.UtcNow;
                    _consecutiveErrors = 0;
                    mConnected = true;
                }
                catch (Exception ex)
                {
                    Tools.Log($"[Hummingbird] Failed InitHummingbird: {ex.Message}", Tools.ELogType.SystemLog);
                    mConnected = false;
                }
            }
        }

        private void ReceiveMessages(CancellationToken token)
        {
            Tools.Log("[Hummingbird] Message receiving loop started", Tools.ELogType.SystemLog);

            try
            {
                while (!token.IsCancellationRequested)
                {
                    string rcvStr = null;

                    lock (_sync)
                    {
                        if (_subscriberSocket == null || _subscriberSocket.IsDisposed)
                        {
                            Tools.Log("[Hummingbird] Subscriber socket not available", Tools.ELogType.SystemLog);
                            Thread.Sleep(100);
                            continue;
                        }

                        // 비블로킹 수신 시도
                        if (_subscriberSocket.TryReceiveFrameString(TimeSpan.FromMilliseconds(100), out rcvStr))
                        {
                            // 락 안에서 처리하지 않음 - 데드락 방지
                        }
                    }

                    // 락 바깥에서 메시지 처리 - Tools.Log()의 Dispatcher 호출과 lock 충돌 방지
                    if (!string.IsNullOrEmpty(rcvStr))
                    {
                        ProcessMessage(rcvStr);
                        lock (_sync)
                        {
                            _lastRxUtc = DateTime.UtcNow;
                            _consecutiveErrors = 0;
                        }
                    }

                    Thread.Sleep(10); // CPU 부하 방지
                }
            }
            catch (Exception ex)
            {
                Tools.Log($"[Hummingbird] Error in ReceiveMessages: {ex.Message}", Tools.ELogType.SystemLog);
                _consecutiveErrors++;
                if (_consecutiveErrors >= MaxErrorsBeforeReconnect)
                {
                    ReconnectHummingbird("receive exception threshold");
                }
            }

            Tools.Log("[Hummingbird] Message receiving loop stopped", Tools.ELogType.SystemLog);
        }

        private void ProcessMessage(string message)
        {
            try
            {
                //Tools.Log($"[Hummingbird] Received: {message}", Tools.ELogType.ActionLog);

                // 토픽과 JSON 데이터 분리
                string topic = string.Empty;
                string jsonData = message;

                // "토픽 JSON" 형식 파싱
                if (message.Contains(" ") && message.Contains("{"))
                {
                    int jsonStartIndex = message.IndexOf("{");
                    topic = message.Substring(0, jsonStartIndex).Trim();
                    jsonData = message.Substring(jsonStartIndex);
                }
                else if (message.Contains("{"))
                {
                    // JSON만 있는 경우
                    jsonData = message;
                }

                // 기존 LIVOXModel 구조에 맞춰 JSON 파싱
                LIVOXModel eventModel = new LIVOXModel
                {
                    topic = topic,
                    responseCode = 0,
                    width = 0,
                    height = 0,
                    length = 0,
                    result = 0,
                    points = string.Empty
                };

                // JSON 파싱 시도
                if (jsonData.Contains("{"))
                {
                    try
                    {
                        var jsonObject = JObject.Parse(jsonData);

                        // width, height, length, result, points 파싱
                        if (jsonObject["width"] != null)
                            eventModel.width = (int)jsonObject["width"];

                        if (jsonObject["height"] != null)
                            eventModel.height = (int)jsonObject["height"];

                        if (jsonObject["length"] != null)
                            eventModel.length = (int)jsonObject["length"];

                        if (jsonObject["result"] != null)
                            eventModel.result = (int)jsonObject["result"];

                        if (jsonObject["points"] != null)
                            eventModel.points = jsonObject["points"].ToString();

                        if (jsonObject["responseCode"] != null)
                            eventModel.responseCode = (int)jsonObject["responseCode"];

                        //Tools.Log($"[Hummingbird] Parsed - width:{eventModel.width}, height:{eventModel.height}, length:{eventModel.length}",
                        //    Tools.ELogType.ActionLog);
                    }
                    catch (Exception jsonEx)
                    {
                        Tools.Log($"[Hummingbird] JSON parse error: {jsonEx.Message}", Tools.ELogType.SystemLog);
                    }
                }

                // HummingbirdEvent로 발행
                _eventAggregator.GetEvent<HummingbirdEvent>().Publish(eventModel);

                //Tools.Log($"[Hummingbird] Published HummingbirdEvent - Topic: '{topic}'", Tools.ELogType.SystemLog);
            }
            catch (Exception ex)
            {
                Tools.Log($"[Hummingbird] Failed to process message: {ex.Message}", Tools.ELogType.SystemLog);
            }
        }

        private void StartHealthCheck()
        {
            if (mCheckConnTimer != null) return;

            mCheckConnTimer = new DispatcherTimer();
            mCheckConnTimer.Interval = TimeSpan.FromSeconds(5);
            mCheckConnTimer.Tick += (_, __) =>
            {
                try
                {
                    if (_reconnecting) return;

                    bool needReconnect = false;
                    lock (_sync)
                    {
                        if (_subscriberSocket == null || _subscriberSocket.IsDisposed)
                        {
                            needReconnect = true;
                        }
                        else
                        {
                            // 10초 이상 메시지가 없으면 경고 (재연결은 하지 않음)
                            var now = DateTime.UtcNow;
                            if ((now - _lastRxUtc).TotalSeconds > 10)
                            {
                                Tools.Log($"[Hummingbird] No message received for {(now - _lastRxUtc).TotalSeconds:F1}s", Tools.ELogType.SystemLog);
                            }
                        }
                    }

                    if (needReconnect)
                    {
                        ReconnectHummingbird("healthcheck socket disposed");
                    }
                }
                catch (Exception ex)
                {
                    Tools.Log($"[Hummingbird] HealthCheck error: {ex.Message}", Tools.ELogType.SystemLog);
                }
            };
            mCheckConnTimer.Start();
            //Tools.Log($"[Hummingbird] HealthCheck started (5s interval)", Tools.ELogType.SystemLog);
        }

        private void ReconnectHummingbird(string reason)
        {
            if (_reconnecting) return;
            _reconnecting = true;

            Tools.Log($"[Hummingbird] Reconnecting due to: {reason}", Tools.ELogType.SystemLog);
            try
            {
                lock (_sync)
                {
                    CloseSocketsNoThrow();
                }
                Thread.Sleep(150);
                InitHummingbird();
            }
            catch (Exception ex)
            {
                Tools.Log($"[Hummingbird] Reconnect failed: {ex.Message}", Tools.ELogType.SystemLog);
            }
            finally
            {
                _reconnecting = false;
            }
        }

        private void CloseSocketsNoThrow()
        {
            try
            {
                _cancellationTokenSource?.Cancel();
                _subscriberSocket?.Dispose();
            }
            catch { }

            _subscriberSocket = null;
            mConnected = false;
        }

        public void Stop()
        {
            try
            {
                mCheckConnTimer?.Stop();
                _cancellationTokenSource?.Cancel();
                _subscriberSocket?.Dispose();
                Tools.Log("[Hummingbird] Stopped", Tools.ELogType.SystemLog);
            }
            catch (Exception ex)
            {
                Tools.Log($"[Hummingbird] Error stopping: {ex.Message}", Tools.ELogType.SystemLog);
            }
        }
    }
}