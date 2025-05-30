using Prism.Events;
using Prism.Ioc;
using Prism.Modularity;
using WATA.LIS.Core;
using WATA.LIS.Core.Interfaces;
using WATA.LIS.Core.Model;
using WATA.LIS.Core.Model.SystemConfig;
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

            DistanceConfigModel _DistanceConfig = (DistanceConfigModel)_distancemodel;

            if (_DistanceConfig.distance_enable == 0)
            {
                return;
            }

            if (_DistanceConfig.model_name == "TF_Luna")
            {
                //TF_Luna TeraBeeSensor = new TF_Luna(_eventAggregator, _distancemodel);
                //TeraBeeSensor.SerialInit();
            }
            else if (_DistanceConfig.model_name == "TF_Mini")
            {
                TF_Mini TF_Mini = new TF_Mini(_eventAggregator, _distancemodel);
                TF_Mini.SerialInit();
            }
            else if (_DistanceConfig.model_name == "SICK_LONG")
            {
                SICK_LONG Sensor = new SICK_LONG(_eventAggregator, _distancemodel);
                Sensor.SerialInit();
            }
            else if (_DistanceConfig.model_name == "SICK_SHORT")
            {
                SICK_SHORT Sensor = new SICK_SHORT(_eventAggregator, _distancemodel);
                Sensor.SerialInit();
            }
            else
            {
                DistanceSensor TeraBeeSensor = new DistanceSensor(_eventAggregator, _distancemodel);
                TeraBeeSensor.SerialInit();
            }

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