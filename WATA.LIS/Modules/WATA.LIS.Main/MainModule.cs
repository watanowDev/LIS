using Prism.Ioc;
using Prism.Modularity;
using Prism.Regions;
using WATA.LIS.Core;
using WATA.LIS.Main.Views;
using WATA.LIS.Core.Common;
using System;

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
            // Region이 완전히 준비된 후 직접 뷰 등록 방식으로 시도
            try
            {
                Tools.Log("[MainModule] Using RegisterViewWithRegion for MainUI", Tools.ELogType.SystemLog);
                _regionManager.RegisterViewWithRegion(RegionNames.Content_Main, typeof(MainUI));
                Tools.Log("[MainModule] MainUI registered successfully", Tools.ELogType.SystemLog);
            }
            catch (Exception ex)
            {
                Tools.Log($"[MainModule] Registration exception: {ex.Message}", Tools.ELogType.SystemLog);
            }
        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            // TopStatusBar는 즉시 등록 가능 (RegisterViewWithRegion 사용)
            _regionManager.RegisterViewWithRegion(RegionNames.TopStatusBar, typeof(TopBarUI));

            // MainUI를 네비게이션용으로도 등록 (혹시 다른 곳에서 사용할 수 있으니)
            containerRegistry.RegisterForNavigation<MainUI>();

            // BottomUIRegion에 NavigationBarUI 등록
            _regionManager.RegisterViewWithRegion(RegionNames.BottomUIRegion, typeof(NavigationBarUI));
        }
    }
}