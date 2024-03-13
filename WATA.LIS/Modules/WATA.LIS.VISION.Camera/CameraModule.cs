using Prism.Events;
using Prism.Ioc;
using Prism.Modularity;
using Prism.Regions;
using WATA.LIS.Core;
using WATA.LIS.Core.Interfaces;
using WATA.LIS.Core.Model.SystemConfig;
using WATA.LIS.VISION.Camera.Camera;
using WATA.LIS.VISION.Camera.Views;

namespace WATA.LIS.VISION.Camera
{
    public class CameraModule : IModule
    {
        private readonly IRegionManager _regionManager;
        private readonly IEventAggregator _eventAggregator;
        private readonly IVisionModel _visionModel;

        public CameraModule(IRegionManager regionManager, IEventAggregator eventAggregator, IVisionModel visionModel, IMainModel main)
        {
            _regionManager = regionManager;
            _eventAggregator = eventAggregator;
            _visionModel = visionModel;


            AstraCamera camera = new AstraCamera(_eventAggregator, _visionModel, main);
            camera.Init();
        
        }

        public void OnInitialized(IContainerProvider containerProvider)
        {
        
        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterForNavigation<VisionCamera>(RegionNames.Content_Camera);
        }
    }
}