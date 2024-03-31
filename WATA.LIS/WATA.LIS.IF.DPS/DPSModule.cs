using Prism.Events;
using Prism.Ioc;
using Prism.Modularity;
using Prism.Regions;
using WATA.LIS.Core;
using WATA.LIS.Core.Interfaces;
using WATA.LIS.Core.Model.SystemConfig;
using WATA.LIS.IF.DPS.ViewModels;
using WATA.LIS.IF.DPS.Views;
using WATA.LIS.TCPSocket;

namespace WATA.LIS.IF.DPS
{
    public class DPSModule : IModule
    {
        private readonly IEventAggregator _eventAggregator;


  
        public DPSModule(IEventAggregator eventAggregator , IDPSModel dps_model)
        {
            _eventAggregator = eventAggregator;

            DPSConfigModel dps_config = (DPSConfigModel)dps_model;
            TcpClientSimple tcpobj = new TcpClientSimple(_eventAggregator, dps_config.IP, dps_config.PORT);
            tcpobj.InitAsync();
           
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
