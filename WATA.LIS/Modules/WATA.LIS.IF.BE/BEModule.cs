using Prism.Ioc;
using Prism.Modularity;
using Prism.Regions;
using WATA.LIS.Core;
using WATA.LIS.IF.BE.Views;

namespace WATA.LIS.IF.BE
{
    public class BEModule : IModule
    {
        private readonly IRegionManager _regionManager;

        public BEModule(IRegionManager regionManager)
        {
            _regionManager = regionManager;
        }

        public void OnInitialized(IContainerProvider containerProvider)
        {

        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterForNavigation<BackEnd>(RegionNames.Content_BackEnd);
        }
    }
}