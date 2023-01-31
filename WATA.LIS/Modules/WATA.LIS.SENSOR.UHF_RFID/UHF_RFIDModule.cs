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
using WATA.LIS.Core.Services;
using WATA.LIS.Core.Model.RFID;
using Apulsetech.Rfid.Vendor.Tag.Sensor.Rfmicron;
using System.Windows.Media;
using WATA.LIS.Core.Interfaces;
using System.Text.Json.Serialization;
using WATA.LIS.Core.Model.VISION;
//using WATA.LIS.Core.Interfaces;

namespace WATA.LIS.SENSOR.UHF_RFID
{
    public class UHF_RFIDModule : IModule
    {
 


        private readonly IEventAggregator _eventAggregator;
        private readonly IRFIDModel _rfidmodel;
        
        public UHF_RFIDModule(IEventAggregator eventAggregator, IRFIDModel rfidmodel)
        {
            _eventAggregator = eventAggregator;
            _rfidmodel = rfidmodel;
           //WPSControl rfid = new WPSControl(_eventAggregator);
           //rfid.Init();

           ApulseTechControl rfid = new ApulseTechControl(_eventAggregator , _rfidmodel);
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