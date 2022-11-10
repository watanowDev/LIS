using Prism.Events;
using Prism.Ioc;
using Prism.Modularity;
using Prism.Regions;
using WATA.LIS.Core;
using WATA.LIS.IF.BE.REST;
using WATA.LIS.IF.BE.Views;

namespace WATA.LIS.IF.BE
{
    public class BEModule : IModule
    {

        private readonly IEventAggregator _eventAggregator;
        public BEModule(IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;
            RestAPI restAPI = new RestAPI(_eventAggregator);
            restAPI.init();
        }

        public void OnInitialized(IContainerProvider containerProvider)
        {

        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterForNavigation<BackEnd>(RegionNames.Content_BackEnd);
        }
    }
}