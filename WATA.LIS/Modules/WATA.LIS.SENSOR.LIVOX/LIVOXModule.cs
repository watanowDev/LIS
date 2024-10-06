using Prism.Events;
using Prism.Ioc;
using Prism.Modularity;
using WATA.LIS.Core;
using WATA.LIS.Core.Interfaces;
using WATA.LIS.Core.Model;
using WATA.LIS.SENSOR.LIVOX.MQTT;
using WATA.LIS.SENSOR.LIVOX.Views;

namespace WATA.LIS.SENSOR.LIVOX
{
    public class LIVOXModule : IModule
    {
        private readonly IEventAggregator _eventAggregator;
        private readonly ILivoxModel _livoxmodel;

        public LIVOXModule(IEventAggregator eventAggregator, ILivoxModel livoxmodel)
        {
            _eventAggregator = eventAggregator;
            _livoxmodel = livoxmodel;

            PubSub pubsub = new PubSub(_eventAggregator, _livoxmodel);
            pubsub.Init();
        }

        public void OnInitialized(IContainerProvider containerProvider)
        {

        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterForNavigation<ViewA>(RegionNames.Content_LIVOX);
        }
    }
}