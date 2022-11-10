using Prism.Events;
using Prism.Ioc;
using Prism.Modularity;
using Prism.Regions;
using System.Net.Sockets;
using System.Threading;
using System;
using WATA.LIS.Core;
using WATA.LIS.SENSOR.UHF_RFID.Views;
using WATA.LIS.SENSOR.UHF_RFID.Sensor;

namespace WATA.LIS.SENSOR.UHF_RFID
{
    public class UHF_RFIDModule : IModule
    {
        private readonly IEventAggregator _eventAggregator;

        public UHF_RFIDModule(IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;
            RFID_SENSOR rfid = new RFID_SENSOR(_eventAggregator);
            rfid.Init();
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