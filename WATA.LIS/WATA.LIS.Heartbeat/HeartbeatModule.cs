using Prism.Events;
using Prism.Ioc;
using Prism.Modularity;
using WATA.LIS.Core;
using WATA.LIS.Core.Interfaces;
using WATA.LIS.Core.Model;
using WATA.LIS.Core.Model.SystemConfig;
using Microsoft.Extensions.Logging;
using WATA.LIS.Heartbeat.Services;

namespace WATA.LIS.Heartbeat
{
    public class HeartbeatModule : IModule
    {
        private readonly IEventAggregator _eventAggregator;
        private readonly ILogger<HeartbeatService> _logger;
        private HeartbeatService _heartbeatService;

        public HeartbeatModule(IEventAggregator eventAggregator, ILogger<HeartbeatService> logger)
        {
            _eventAggregator = eventAggregator;
            _logger = logger;
        }

        public void OnInitialized(IContainerProvider containerProvider)
        {
            // HeartbeatService 초기화 및 시작
            _heartbeatService = new HeartbeatService(_logger);
            _heartbeatService.Start();
        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            // 필요한 경우 다른 서비스나 뷰를 등록할 수 있습니다.
        }
    }
}