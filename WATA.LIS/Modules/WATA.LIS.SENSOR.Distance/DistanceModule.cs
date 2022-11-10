using Prism.Events;
using Prism.Ioc;
using Prism.Modularity;
using Prism.Regions;
using WATA.LIS.Core;
using WATA.LIS.SENSOR.Distance.Views;
using WATA.LIS.SENSOR.Distance.Sensor;
using WATA.LIS.SENSOR.UHF_RFID.Sensor;

namespace WATA.LIS.SENSOR.Distance
{
    public class DistanceModule : IModule
    {
  
        private readonly IEventAggregator _eventAggregator;
        public DistanceModule(IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;
            DistanceSensor TeraBeeSensor = new DistanceSensor(_eventAggregator);
            TeraBeeSensor.SerialInit();
        }

        public void OnInitialized(IContainerProvider containerProvider)
        {
        
        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {  
            containerRegistry.RegisterForNavigation<DistanceCheck>(RegionNames.Content_Distance);
        }
    }
}