using NetMQ.Sockets;
using Prism.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;
using WATA.LIS.Core.Interfaces;
using WATA.LIS.Core.Model.SystemConfig;
using NetMQ;
using WATA.LIS.Core.Common;
using Newtonsoft.Json.Linq;
using System.Threading;
using WATA.LIS.Core.Model;
using WATA.LIS.Core.Model.NAV;
using WATA.LIS.Core.Events.NAVSensor;
using System.Diagnostics;
using static WATA.LIS.Core.Common.Tools;

namespace WATA.LIS.SENSOR.NAV.VisionPosMQTT
{
    public class VisionPos
    {
        private readonly IEventAggregator _eventAggregator;
        private readonly INAVModel _navmodel;

        NAVConfigModel navConfig;

        private DispatcherTimer mCheckConnTimer;
        private DispatcherTimer mSubTimer;
        private bool mConnected = false;
        private string mSubMsg = string.Empty;

        public SubscriberSocket _subscriberSocket;
        private CancellationTokenSource _cancellationTokenSource;

        public VisionPos(IEventAggregator eventAggregator, INAVModel navmodel)
        {
            _eventAggregator = eventAggregator;
            _navmodel = navmodel;
            navConfig = (NAVConfigModel)_navmodel;
        }

        public void Init()
        {
            InitVisionPos();
        }

        private void InitVisionPos()
        {
            try
            {
                _subscriberSocket = new SubscriberSocket();
                _subscriberSocket.Connect("tcp://localhost:5090");

                // 타임아웃 설정 (예: 5초)
                _subscriberSocket.Options.HeartbeatTimeout = TimeSpan.FromSeconds(3);
                _subscriberSocket.SubscribeToAnyTopic();

                _cancellationTokenSource = new CancellationTokenSource();
                var token = _cancellationTokenSource.Token;

                Task.Factory.StartNew(() => ReceiveMessages(token), token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            }
            catch (Exception ex)
            {
                Tools.Log($"Failed InitVisionPubSub: {ex.Message}", Tools.ELogType.SystemLog);
            }
        }

        private void ReceiveMessages(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    string message = _subscriberSocket.ReceiveFrameString();
                    if (message != null)
                    {
                        var data = message.Split(',');
                        if (data.Length == 3 &&
                            long.TryParse(data[0], out long visionX) &&
                            long.TryParse(data[1], out long visionY) &&
                            long.TryParse(data[2], out long visionT))
                        {
                            var visionPosModel = new NAVSensorModel
                            {
                                naviX = visionX,
                                naviY = visionY,
                                naviT = visionT,
                                result = "1"
                            };

                            _eventAggregator.GetEvent<NAVSensorEvent>().Publish(visionPosModel);
                        }
                        else
                        {
                            Tools.Log("Invalid message format received", Tools.ELogType.SystemLog);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Tools.Log($"Error in ReceiveMessages: {ex.Message}", Tools.ELogType.SystemLog);
            }
        }

        public void Stop()
        {
            _cancellationTokenSource?.Cancel();
            _subscriberSocket?.Dispose();
        }
    }
}