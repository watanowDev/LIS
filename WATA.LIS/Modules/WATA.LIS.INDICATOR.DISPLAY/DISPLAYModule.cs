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
        private readonly IDisplayModel _displaymodel;


        //ZMQServer _IndicatorServer;
        private string _strAcsIp = "Any";
        private int _serverPort = 8051;



        public DISPLAYModule(IEventAggregator eventAggregator, IDisplayModel displayModel)
        {
            _eventAggregator = eventAggregator;
            _displaymodel = displayModel;

            DisplayConfigModel _displayConfig = (DisplayConfigModel)_displaymodel;

            // ✅ 디버깅: display_enable 값 확인 로그
            Tools.Log($"[DISPLAY MODULE] display_enable = {_displayConfig.display_enable}", Tools.ELogType.SystemLog);
            Tools.Log($"[DISPLAY MODULE] display_type = {_displayConfig.display_type ?? "null"}", Tools.ELogType.SystemLog);
            Tools.Log($"[DISPLAY MODULE] IDisplayModel instance type = {_displaymodel?.GetType().FullName ?? "null"}", Tools.ELogType.SystemLog);

            if (_displayConfig.display_enable == 0)
            {
                Tools.Log("[DISPLAY MODULE] display_enable is 0, TCP Server will NOT start", Tools.ELogType.SystemLog);
                return;
            }

            Tools.Log("[DISPLAY MODULE] Starting TcpServerSimple...", Tools.ELogType.SystemLog);
            TcpServerSimple _tcpServer = new TcpServerSimple(_eventAggregator);
            _tcpServer.initAsync();
            Tools.Log("[DISPLAY MODULE] TcpServerSimple initiated", Tools.ELogType.SystemLog);
        }

        public void OnInitialized(IContainerProvider containerProvider)
        {
            Tools.Log("[DISPLAY MODULE] OnInitialized called", Tools.ELogType.SystemLog);
        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            Tools.Log("[DISPLAY MODULE] RegisterTypes called", Tools.ELogType.SystemLog);
            containerRegistry.RegisterForNavigation<DisplayView>(RegionNames.Content_Indicator);
        }
    }
}