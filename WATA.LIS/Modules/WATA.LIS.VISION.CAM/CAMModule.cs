using Prism.Events;
using Prism.Ioc;
using Prism.Modularity;
using WATA.LIS.Core;
using Prism.Regions;
using WATA.LIS.VISION.CAM.Views;
using WATA.LIS.Core.Interfaces;
using WATA.LIS.Core.Model.SystemConfig;
using WATA.LIS.VISION.CAM.Camera;

namespace WATA.LIS.VISION.CAM
{
    public class CAMModule : IModule
    {
        private readonly IEventAggregator _eventAggregator;
        private readonly IQRCameraModel _qrcameramodel;

        public CAMModule(IEventAggregator eventAggregator, IQRCameraModel qrcameramodel)
        {
            _eventAggregator = eventAggregator;
            _qrcameramodel = qrcameramodel;

            VisionCamConfigModel qrcamera_config = (VisionCamConfigModel)qrcameramodel;

            if (qrcamera_config.vision_enable == 0)
            {
                return;
            }

            if (qrcamera_config.vision_name == "HikVision")
            {
                HIKVISION qrcamera = new HIKVISION(_eventAggregator, _qrcameramodel);
                qrcamera.Init();
            }
        }

        public void OnInitialized(IContainerProvider containerProvider)
        {

        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterForNavigation<CamView>(RegionNames.Content_VisionCam);
        }
    }
}