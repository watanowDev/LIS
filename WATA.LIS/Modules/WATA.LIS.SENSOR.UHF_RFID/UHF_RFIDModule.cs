using Prism.Events;
using Prism.Ioc;
using Prism.Modularity;
using Prism.Regions;
using System.Net.Sockets;
using System.Threading;
using System;
using WATA.LIS.Core;
using WATA.LIS.SENSOR.Sensor;
using WATA.LIS.SENSOR.UHF_RFID.Views;


namespace WATA.LIS.SENSOR.UHF_RFID
{
    public class UHF_RFIDModule : IModule
    {
        private readonly IRegionManager _regionManager;
        private readonly IEventAggregator _eventAggregator;


        public UHF_RFIDModule(IRegionManager regionManager, IEventAggregator eventAggregator)
        {
            _regionManager = regionManager;
            _eventAggregator = eventAggregator;
            RFID_SENSOR rfid = new RFID_SENSOR(_eventAggregator);


        }

      

        public void OnInitialized(IContainerProvider containerProvider)
        {

        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterForNavigation<UHFRfid>(RegionNames.Content_RFID);
        }
    }
}