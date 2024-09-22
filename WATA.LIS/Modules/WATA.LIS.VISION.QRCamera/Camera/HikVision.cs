using Prism.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;
using WATA.LIS.Core.Interfaces;
using WATA.LIS.Core.Model.SystemConfig;

namespace WATA.LIS.VISION.QRCamera.Camera
{
    public class HikVision
    {
        private readonly IEventAggregator _eventAggregator;
        private readonly IQRCameraModel _qrcameramodel;

        QRCameraConfigModel qrcameraConfig;

        private DispatcherTimer mCheckConnTimer;
        private DispatcherTimer mGetImageTimer;
        private bool mConnected = false;

        public HikVision(IEventAggregator eventAggregator, IQRCameraModel qrcameramodel, IMainModel main)
        {
            _eventAggregator = eventAggregator;
            _qrcameramodel = qrcameramodel;
            qrcameraConfig = (QRCameraConfigModel)_qrcameramodel;

            MainConfigModel main_config = (MainConfigModel)main;
        }

        public void Init()
        {
            if (qrcameraConfig.qr_enable == 0)
            {
                return;
            }
        }
    }
}
