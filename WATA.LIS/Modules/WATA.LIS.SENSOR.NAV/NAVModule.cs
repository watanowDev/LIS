using Newtonsoft.Json;
using Prism.Events;
using Prism.Ioc;
using Prism.Modularity;
using Prism.Regions;
using System;
using System.IO;
using System.Net;
using System.Runtime.CompilerServices;
using System.Security.Policy;
using System.Text.RegularExpressions;
using WATA.LIS.Core;
using WATA.LIS.Core.Common;
using WATA.LIS.Core.Events.NAVSensor;
using WATA.LIS.Core.Interfaces;
using WATA.LIS.Core.Model.BackEnd;
using WATA.LIS.Core.Model.NAV;
using WATA.LIS.Core.Model.SystemConfig;
using WATA.LIS.SENSOR.NAV.Views;
using WATA.LIS.SENSOR.NAV.VisionPosMQTT;

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

            if (_navConfig.NAV_Enable == 0)
            {
                return;
            }

            if (_navConfig.Type == "NAV")
            {
                NAVSensor navSensor = new NAVSensor(_eventAggregator, _navModel);
                navSensor.Init();
            }
            else if (_navConfig.Type == "VisionPos")
            {
                VisionPos visionPos = new VisionPos(_eventAggregator, _navModel);
                visionPos.Init();
            }
        }

        public void OnInitialized(IContainerProvider containerProvider)
        {

        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterForNavigation<NAV_View>(RegionNames.Content_LiDAR2D);
        }
    }
}