using Prism.Events;
using WATA.LIS.Core.Common;
using WATA.LIS.Core.Events;
using WATA.LIS.Core.Model;

namespace WATA.LIS.Core.Services
{
    public class StatusService : IStatusService
    {
        IEventAggregator _eventAggregator;

        public StatusService(IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;

            _eventAggregator.GetEvent<DistanceSensorEvent>().Subscribe(OnDistanceSensorData, ThreadOption.BackgroundThread, true);

        }

        public void OnDistanceSensorData(DistanceSensorModel data)
        {
            Tools.Log("$DistanceData {}", Tools.ELogType.SystemLog);
        }
    }
}
