using NetMQ.Sockets;
using Prism.Events;
using System;
using System.Diagnostics;
using System.Threading;
using System.Windows.Threading;
using WATA.LIS.Core.Common;
using WATA.LIS.Core.Events.LIVOX;
using WATA.LIS.Core.Interfaces;
using WATA.LIS.Core.Model.LIVOX;
using WATA.LIS.Core.Model.SystemConfig;
using Newtonsoft.Json.Linq;
using NetMQ;
using static WATA.LIS.Core.Common.Tools;

namespace WATA.LIS.SENSOR.LIVOX.MQTT
{
    public class PubSub
    {
        private readonly IEventAggregator _eventAggregator;
        private readonly ILivoxModel _livoxmodel;

        LIVOXConfigModel livoxConfig;

        private DispatcherTimer mCheckConnTimer;
        private bool mConnected = false;

        public PublisherSocket _publisherSocket;
        public SubscriberSocket _subscriberSocket;

        private readonly object _sync = new object();
        private volatile bool _reconnecting = false;

        private const string PubEndpoint = "tcp://127.0.0.1:5002";
        private const string SubEndpoint = "tcp://127.0.0.1:5001";
        private static readonly TimeSpan RxTimeout = TimeSpan.FromMilliseconds(500);
        private static readonly TimeSpan ResponseWindow = TimeSpan.FromSeconds(2); // 응답 기대 시간

        private const int MaxErrorsBeforeReconnect = 3;

        private int _consecutiveErrors = 0;
        private DateTime _lastRxUtc = DateTime.MinValue;
        private DateTime _lastTxUtc = DateTime.MinValue;
        private volatile bool _awaitingResponse = false;

        Stopwatch m_stopwatch;

        public PubSub(IEventAggregator eventAggregator, ILivoxModel livoxmodel)
        {
            _eventAggregator = eventAggregator;
            _livoxmodel = livoxmodel;
            livoxConfig = (LIVOXConfigModel)_livoxmodel;

            _eventAggregator.GetEvent<CallDataEvent>().Subscribe(OnCallDataEvent, ThreadOption.BackgroundThread, true);
        }

        public void Init()
        {
            InitLivox();
            StartHealthCheck();
        }

        private void InitLivox()
        {
            lock (_sync)
            {
                try
                {
                    CloseSocketsNoThrow();

                    _publisherSocket = new PublisherSocket();
                    _publisherSocket.Options.SendHighWatermark = 1000;
                    _publisherSocket.Bind(PubEndpoint);

                    Tools.Log($"[LIVOX] Init publisher at {PubEndpoint}", Tools.ELogType.SystemLog);

                    _subscriberSocket = new SubscriberSocket();
                    _subscriberSocket.Options.ReceiveHighWatermark = 1000;
                    _subscriberSocket.Connect(SubEndpoint);
                    _subscriberSocket.Subscribe("MID360>LIS"); // subscribe once

                    Tools.Log($"[LIVOX] Init subscriber at {SubEndpoint}", Tools.ELogType.SystemLog);

                    _lastRxUtc = DateTime.UtcNow;
                    _consecutiveErrors = 0;
                    mConnected = true;
                }
                catch (Exception ex)
                {
                    Tools.Log($"[LIVOX] Failed InitLivox PubSub: {ex.Message}", Tools.ELogType.SystemLog);
                    mConnected = false;
                }
            }
        }

        private void StartHealthCheck()
        {
            if (mCheckConnTimer != null) return;

            mCheckConnTimer = new DispatcherTimer();
            mCheckConnTimer.Interval = TimeSpan.FromSeconds(1);
            mCheckConnTimer.Tick += (_, __) =>
            {
                try
                {
                    if (_reconnecting) return;

                    bool needReconnect = false;
                    lock (_sync)
                    {
                        if (_publisherSocket == null || _publisherSocket.IsDisposed ||
                            _subscriberSocket == null || _subscriberSocket.IsDisposed)
                        {
                            needReconnect = true;
                        }
                        else if (_awaitingResponse)
                        {
                            // 요청을 보낸 상태에서 응답이 일정 시간 내 오지 않으면 재연결
                            var now = DateTime.UtcNow;
                            if ((now - _lastTxUtc) > ResponseWindow && (now - _lastRxUtc) > ResponseWindow)
                                needReconnect = true;
                        }
                    }

                    if (needReconnect)
                    {
                        ReconnectLivox("healthcheck awaiting-response timeout/disposed");
                    }
                }
                catch (Exception ex)
                {
                    Tools.Log($"[LIVOX] HealthCheck error: {ex.Message}", Tools.ELogType.SystemLog);
                }
            };
            mCheckConnTimer.Start();
            Tools.Log($"[LIVOX] HealthCheck started (1s)", Tools.ELogType.SystemLog);
        }

        private void ReconnectLivox(string reason)
        {
            if (_reconnecting) return;
            _reconnecting = true;

            Tools.Log($"[LIVOX] Reconnecting due to: {reason}", Tools.ELogType.SystemLog);
            try
            {
                lock (_sync)
                {
                    CloseSocketsNoThrow();
                }
                Thread.Sleep(150); // short cooldown
                InitLivox();
            }
            catch (Exception ex)
            {
                Tools.Log($"[LIVOX] Reconnect failed: {ex.Message}", Tools.ELogType.SystemLog);
            }
            finally
            {
                _reconnecting = false;
            }
        }

        private void CloseSocketsNoThrow()
        {
            try { _publisherSocket?.Dispose(); } catch { }
            try { _subscriberSocket?.Dispose(); } catch { }
            _publisherSocket = null;
            _subscriberSocket = null;
            mConnected = false;
        }

        private void OnCallDataEvent()
        {
            _awaitingResponse = true;
            m_stopwatch = new Stopwatch();
            m_stopwatch.Start();

            bool isSendZero = false;

            int getLivoxctn = 0;
            while (getLivoxctn < 10)
            {
                SendToLivox(1);
                if (GetSizeData() == true)
                {
                    SendToLivox(0);
                    isSendZero = true;
                    break;
                }
                getLivoxctn++;
                Thread.Sleep(100);
            }

            if (isSendZero == false)
            {
                SendToLivox(0);
            }

            m_stopwatch.Stop();
            Tools.Log($"Size Measuring Time : {m_stopwatch.ElapsedMilliseconds}ms", ELogType.ActionLog);
            _awaitingResponse = false;
        }

        public void SendToLivox(int commandNum)
        {
            try
            {
                string message = $"LIS>MID360,{commandNum}"; // 1: request, 0: ack

                lock (_sync)
                {
                    if (_publisherSocket == null || _publisherSocket.IsDisposed)
                        throw new InvalidOperationException("Publisher socket not available");
                    _publisherSocket.SendFrame(message);
                    _lastTxUtc = DateTime.UtcNow;
                }

                Tools.Log($"SendToLivox : {message}", Tools.ELogType.SystemLog);
            }
            catch (Exception ex)
            {
                Tools.Log($"[LIVOX] Failed SendToLivox : {ex.Message}", Tools.ELogType.SystemLog);
                _consecutiveErrors++;
                if (_consecutiveErrors >= MaxErrorsBeforeReconnect)
                {
                    ReconnectLivox("send failure threshold");
                }
            }
        }

        private bool GetSizeData()
        {
            bool ret = false;
            try
            {
                string rcvStr = null;
                bool got = false;

                var deadline = DateTime.UtcNow + RxTimeout;
                lock (_sync)
                {
                    if (_subscriberSocket == null || _subscriberSocket.IsDisposed)
                        throw new InvalidOperationException("Subscriber socket not available");

                    // poll for up to RxTimeout
                    while (DateTime.UtcNow < deadline)
                    {
                        if (_subscriberSocket.TryReceiveFrameString(out rcvStr))
                        {
                            got = true;
                            break;
                        }
                        Thread.Sleep(10);
                    }
                }

                if (!got)
                {
                    Tools.Log("[LIVOX] receive timeout", Tools.ELogType.SystemLog);
                    _consecutiveErrors++;
                    if (_consecutiveErrors >= MaxErrorsBeforeReconnect)
                    {
                        ReconnectLivox("receive timeout threshold");
                    }
                    return false;
                }

                if (string.IsNullOrEmpty(rcvStr) || !rcvStr.Contains("MID360>LIS"))
                {
                    // unexpected topic
                    return false;
                }

                if (rcvStr.Contains("height") && rcvStr.Contains("width") && rcvStr.Contains("length") && rcvStr.Contains("result"))
                {
                    var jsonString = rcvStr.Substring(rcvStr.IndexOf("{"));
                    var jsonObject = JObject.Parse(jsonString);

                    LIVOXModel eventModel = new LIVOXModel
                    {
                        topic = "MID360>LIS",
                        responseCode = 0,
                        width = (int)jsonObject["width"],
                        height = (int)jsonObject["height"],
                        length = (int)jsonObject["length"],
                        result = (int)jsonObject["result"],
                        points = jsonObject["points"]?.ToString() ?? string.Empty
                    };

                    _eventAggregator.GetEvent<LIVOXEvent>().Publish(eventModel);
                    Tools.Log($"height:{eventModel.height}, width:{eventModel.width}, depth:{eventModel.length}", Tools.ELogType.ActionLog);

                    _lastRxUtc = DateTime.UtcNow;
                    _consecutiveErrors = 0;
                    ret = true;
                }
                else
                {
                    _consecutiveErrors++;
                    if (_consecutiveErrors >= MaxErrorsBeforeReconnect)
                    {
                        ReconnectLivox("invalid payload threshold");
                    }
                }
            }
            catch (Exception ex)
            {
                Tools.Log($"[LIVOX] Receive exception: {ex.Message}", Tools.ELogType.SystemLog);
                _consecutiveErrors++;
                if (_consecutiveErrors >= MaxErrorsBeforeReconnect)
                {
                    ReconnectLivox("receive exception threshold");
                }
            }

            return ret;
        }
    }
}
