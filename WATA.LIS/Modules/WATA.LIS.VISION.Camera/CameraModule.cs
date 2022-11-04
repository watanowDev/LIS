using Prism.Ioc;
using Prism.Modularity;
using Prism.Regions;
using WATA.LIS.Core;
using WATA.LIS.VISION.Camera.Views;

namespace WATA.LIS.VISION.Camera
{
    public class CameraModule : IModule
    {
        private readonly IRegionManager _regionManager;

        public CameraModule(IRegionManager regionManager)
        {
            _regionManager = regionManager;
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