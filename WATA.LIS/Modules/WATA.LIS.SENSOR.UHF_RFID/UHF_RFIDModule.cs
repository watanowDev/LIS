using Prism.Events;
using Prism.Ioc;
using Prism.Modularity;
using WATA.LIS.Core;
using WATA.LIS.SENSOR.UHF_RFID.Views;
using WATA.LIS.SENSOR.UHF_RFID.Sensor;
using WATA.LIS.Core.Interfaces;
using WATA.LIS.Core.Model.SystemConfig;

namespace WATA.LIS.SENSOR.UHF_RFID
{
    public class UHF_RFIDModule : IModule
    {
 


        private readonly IEventAggregator _eventAggregator;
        private readonly IRFIDModel _rfidmodel;
        
        public UHF_RFIDModule(IEventAggregator eventAggregator, IRFIDModel rfidmodel, IMainModel main)
        {
            _eventAggregator = eventAggregator;
            _rfidmodel = rfidmodel;

            MainConfigModel main_config = (MainConfigModel)main;
            RFIDConfigModel rfid_config = (RFIDConfigModel)rfidmodel;

            if (main_config.device_type == "fork_lift_v1")
            {
                //WPSControl rfid = new WPSControl(_eventAggregator);
                //rfid.Init();
            }
            if (rfid_config.rfid_name == "Apulse")
            {
                ApulseTechControl rfid = new ApulseTechControl(_eventAggregator, _rfidmodel, main);
                rfid.Init();
            }
            if (rfid_config.rfid_name == "Keonn2ch")
            {
                Keonn_2ch rfid = new Keonn_2ch(_eventAggregator, _rfidmodel, main);
                rfid.Init();
            }
            if (rfid_config.rfid_name == "Keonn4ch")
            {
                Keonn_4ch rfid = new Keonn_4ch(_eventAggregator, _rfidmodel, main);
                rfid.Init();
            }
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