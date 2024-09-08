using Prism.Events;
using Prism.Ioc;
using Prism.Modularity;
using Prism.Regions;
using System;
using System.Runtime.CompilerServices;
using WATA.LIS.Core;
using WATA.LIS.Core.Common;
using WATA.LIS.Core.Interfaces;
using WATA.LIS.Core.Model.SystemConfig;

namespace WATA.LIS.SENSOR.NAV
{
    public class NAVModule : IModule
    {
        private readonly IRegionManager _regionManager;
        private readonly IEventAggregator _eventAggregator;
        private readonly INAVModel _navModel;

        public NAVModule(IRegionManager regionManager, IEventAggregator eventAggregator, INAVModel navModel)
        {
            _regionManager = regionManager;
            _eventAggregator = eventAggregator;
            _navModel = navModel;

            NAVConfigModel _navConfig = (NAVConfigModel)_navModel;

            if(_navConfig.NAV_Enable == 0)
            {
                return;
            }

            NAVSensor navSensor = new NAVSensor(_eventAggregator, _navModel);
            navSensor.Init();
        }

        public void OnInitialized(IContainerProvider containerProvider)
        {
        
        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
        }
    }
}