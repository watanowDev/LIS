using Prism.Events;

namespace WATA.LIS.Core.Events.System
{
    // ������ JSON(payload)�� ����ؼ� ������(StatusService_WATA)�� �����ϵ��� ��
    public sealed class RestorePickupStateEvent : PubSubEvent<string> { }
}