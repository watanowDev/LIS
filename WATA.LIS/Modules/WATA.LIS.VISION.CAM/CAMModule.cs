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
        private readonly IVisionCamModel _visioncammodel;

        public CAMModule(IEventAggregator eventAggregator, IVisionCamModel visioncammodel)
        {
            _eventAggregator = eventAggregator;
            _visioncammodel = visioncammodel;

            VisionCamConfigModel visioncam_config = (VisionCamConfigModel)visioncammodel;

            if (visioncam_config.vision_enable == 0)
            {
                return;
            }

            if (visioncam_config.vision_name == "HikVision")
            {
                HIKVISION visioncam = new HIKVISION(_eventAggregator, _visioncammodel);
                visioncam.Init();
            }
            else if (visioncam_config.vision_name == "FemtoMega")
            {
                FEMTO_MEGA visioncam = new FEMTO_MEGA(_eventAggregator, _visioncammodel);
                visioncam.Init();
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