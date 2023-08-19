using Prism.Events;
using Prism.Ioc;
using Prism.Modularity;
using WATA.LIS.Core;
using WATA.LIS.Core.Interfaces;
using WATA.LIS.SENSOR.WEIGHT.Sensor;
using WATA.LIS.SENSOR.WEIGHT.Views;

namespace WATA.LIS.SENSOR.WEIGHT
{
    public class WEIGHTModule : IModule
    {
        private readonly IEventAggregator _eventAggregator;
        private readonly IWeightModel _weightmodel;
        public WEIGHTModule(IEventAggregator eventAggregator, IWeightModel weightmodel)
        {
           
            _eventAggregator = eventAggregator;
            _weightmodel = weightmodel;
            ForkPatchSensor SystemEngineering = new ForkPatchSensor(_eventAggregator, _weightmodel);
            SystemEngineering.SerialInit();

        }

        public void OnInitialized(IContainerProvider containerProvider)
        {

        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterForNavigation<WeightView>(RegionNames.Content_Weight);
        }
    }
}