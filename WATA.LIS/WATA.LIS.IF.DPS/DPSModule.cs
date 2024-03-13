using Prism.Events;
using Prism.Ioc;
using Prism.Modularity;
using Prism.Regions;
using WATA.LIS.Core;
using WATA.LIS.IF.DPS.ViewModels;
using WATA.LIS.IF.DPS.Views;

namespace WATA.LIS.IF.DPS
{
    public class DPSModule : IModule
    {
        private readonly IEventAggregator _eventAggregator;


  
        public DPSModule(IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;

            DPSViewModel dpsmodel = new DPSViewModel();


        }

        public void OnInitialized(IContainerProvider containerProvider)
        {

        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterForNavigation<DPSView>(RegionNames.Content_DPS);
        }
    }
}
