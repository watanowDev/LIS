using Prism.Events;
using Prism.Ioc;
using Prism.Modularity;
using Prism.Regions;
using WATA.LIS.Core.Common;
using WATA.LIS.Core.Interfaces;
using WATA.LIS.Core.Model.SystemConfig;
using WATA.LIS.Core;
using WATA.LIS.INDICATOR.DISPLAY.Views;
using WATA.LIS.TCPSocket;
using WATA.LIS.ZMQ;

namespace WATA.LIS.INDICATOR.DISPLAY
{
    public class DISPLAYModule : IModule
    {

        private readonly IEventAggregator _eventAggregator;


        //ZMQServer _IndicatorServer;
        private string _strAcsIp = "Any";
        private int _serverPort = 8051;



        public DISPLAYModule(IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;

            TcpServerSimple _tcpServer = new TcpServerSimple(_eventAggregator);
            _tcpServer.initAsync();
        }

        public void OnInitialized(IContainerProvider containerProvider)
        {

        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterForNavigation<DisplayView>(RegionNames.Content_Indicator);
        }
    }
}