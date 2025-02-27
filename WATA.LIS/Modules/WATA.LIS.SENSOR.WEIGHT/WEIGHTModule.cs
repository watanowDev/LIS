using Prism.Events;
using Prism.Ioc;
using Prism.Modularity;
using WATA.LIS.Core;
using WATA.LIS.Core.Common;
using WATA.LIS.Core.Interfaces;
using WATA.LIS.Core.Model.SystemConfig;
using WATA.LIS.SENSOR.WEIGHT.Sensor;
using WATA.LIS.SENSOR.WEIGHT.Views;
using WATA.LIS.TCPSocket;

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

            WeightConfigModel _weightConfig = (WeightConfigModel)_weightmodel;

            if (_weightConfig.weight_enable == 0)
            {
                return;
            }

            if (_weightConfig.sensor_value == "TJ")
            {
                Tools.Log($"TJ", Tools.ELogType.WeightLog);
                LatchLoadCell china = new LatchLoadCell(_eventAggregator, _weightmodel);
                china.Init();
            }
            else if (_weightConfig.sensor_value == "SE")
            {
                Tools.Log($"SystemEngineering", Tools.ELogType.WeightLog);
                ForkPatchSensor SystemEngineering = new ForkPatchSensor(_eventAggregator, _weightmodel, false);
                SystemEngineering.SerialInit();
            }
            else if (_weightConfig.sensor_value == "SE_ONLY_WEIGHT")
            {
                Tools.Log($"SystemEngineering", Tools.ELogType.WeightLog);
                ForkPatchSensor SystemEngineering = new ForkPatchSensor(_eventAggregator, _weightmodel, true);
                SystemEngineering.SerialInit();
            }
            else if (_weightConfig.sensor_value == "TCP")
            {

                TcpServerSimple _tcpServer = new TcpServerSimple(_eventAggregator);
                _tcpServer.initAsync();
            }
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