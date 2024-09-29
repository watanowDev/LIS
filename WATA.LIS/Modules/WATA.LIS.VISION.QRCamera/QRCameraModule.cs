using Prism.Events;
using Prism.Ioc;
using Prism.Modularity;
using WATA.LIS.Core;
using WATA.LIS.VISION.QRCamera.Views;
using WATA.LIS.VISION.QRCamera.Camera;
using WATA.LIS.Core.Interfaces;
using WATA.LIS.Core.Model.SystemConfig;

namespace WATA.LIS.VISION.QRCamera
{
    public class QRCameraModule : IModule
    {
        private readonly IEventAggregator _eventAggregator;
        private readonly IQRCameraModel _qrcameramodel;

        public QRCameraModule(IEventAggregator eventAggregator, IQRCameraModel qrcameramodel, IMainModel main)
        {
            _eventAggregator = eventAggregator;
            _qrcameramodel = qrcameramodel;

            MainConfigModel main_config = (MainConfigModel)main;
            QRCameraConfigModel qrcamera_config = (QRCameraConfigModel)qrcameramodel;

            if (qrcamera_config.vision_enable == 0)
            {
                return;
            }

            if (qrcamera_config.vision_name == "HikVision")
            {
                HikVision qrcamera = new HikVision(_eventAggregator, _qrcameramodel, main);
                qrcamera.Init();
            }
        }

        public void OnInitialized(IContainerProvider containerProvider)
        {

        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterForNavigation<QRCodeCam>(RegionNames.Content_QRCamera);
        }
    }
}