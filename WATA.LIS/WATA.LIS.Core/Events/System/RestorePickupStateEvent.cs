using Prism.Events;

namespace WATA.LIS.Core.Events.System
{
    // 스냅샷 JSON(payload)을 방송해서 구독자(StatusService_WATA)가 복원하도록 함
    public sealed class RestorePickupStateEvent : PubSubEvent<string> { }
}