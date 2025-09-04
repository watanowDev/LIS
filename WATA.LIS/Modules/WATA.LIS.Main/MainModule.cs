using Prism.Ioc;
using Prism.Modularity;
using Prism.Regions;
using System;
using System.Diagnostics;
using WATA.LIS.Core;
using WATA.LIS.Main.Views;
using static System.Net.Mime.MediaTypeNames;

namespace WATA.LIS.Main
{
    public class MainModule : IModule
    {
        private readonly IRegionManager _regionManager;

        public MainModule(IRegionManager regionManager)
        {
            _regionManager = regionManager;
        }

        public void OnInitialized(IContainerProvider containerProvider)
        {
        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            _regionManager.RegisterViewWithRegion(RegionNames.TopStatusBar, typeof(TopBarUI));

            containerRegistry.RegisterForNavigation<MainUI>();
            _regionManager.RequestNavigate(RegionNames.Content_Main, "MainUI");
            containerRegistry.RegisterForNavigation<MainUI>(RegionNames.Content_Main);

            // Bottom bar removed per request
            //containerRegistry.RegisterForNavigation<BottomBarUI>();
            //_regionManager.RequestNavigate(RegionNames.BottomUIRegion, "BottomBarUI");
        }
    }
}