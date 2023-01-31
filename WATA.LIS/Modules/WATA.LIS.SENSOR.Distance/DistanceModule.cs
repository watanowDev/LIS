using Prism.Events;
using Prism.Ioc;
using Prism.Modularity;
using WATA.LIS.Core;
using WATA.LIS.Core.Interfaces;
using WATA.LIS.Core.Model;
using WATA.LIS.SENSOR.Distance.Sensor;
using WATA.LIS.SENSOR.Distance.Views;

namespace WATA.LIS.SENSOR.Distance
{
    public class DistanceModule : IModule
    {
 
        private readonly IEventAggregator _eventAggregator;
        private readonly IDistanceModel _distancemodel;
        public DistanceModule(IEventAggregator eventAggregator, IDistanceModel distancemodel)
        {
            _eventAggregator = eventAggregator;
            _distancemodel = distancemodel;
            DistanceSensor TeraBeeSensor = new DistanceSensor(_eventAggregator, _distancemodel);
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