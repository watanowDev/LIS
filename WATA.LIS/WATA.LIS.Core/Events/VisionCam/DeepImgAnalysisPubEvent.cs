using Prism.Events;
using WATA.LIS.Core.Model.VisionCam;

namespace WATA.LIS.Core.Events.VisionCam
{
    // Publishes a payload containing image bytes and metadata for deep image analysis
    public class DeepImgAnalysisPubEvent : PubSubEvent<DeepImgAnalysisPubModel>
    {
    }
}
