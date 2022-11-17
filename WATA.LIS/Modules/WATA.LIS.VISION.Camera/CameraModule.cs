using Prism.Events;
using Prism.Ioc;
using Prism.Modularity;
using Prism.Regions;
using WATA.LIS.Core;
using WATA.LIS.VISION.Camera.Camera;
using WATA.LIS.VISION.Camera.Views;

namespace WATA.LIS.VISION.Camera
{
    public class CameraModule : IModule
    {
        private readonly IRegionManager _regionManager;
        private readonly IEventAggregator _eventAggregator;

        public CameraModule(IRegionManager regionManager, IEventAggregator eventAggregator)
        {
            _regionManager = regionManager;
            _eventAggregator = eventAggregator;
            AstraCamera camera = new AstraCamera(_eventAggregator);
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