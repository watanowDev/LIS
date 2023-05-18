using Prism.Events;
using Prism.Ioc;
using Prism.Modularity;
using Prism.Regions;
using WATA.LIS.INDICATOR.LED.StatusLED;
using WATA.LIS.INDICATOR.LED.Views;

namespace WATA.LIS.INDICATOR.LED
{
    public class LEDModule : IModule
    {
        public LEDModule(IRegionManager regionManager, IEventAggregator eventAggregator)
        {
            BlinkStickSquare LED = new BlinkStickSquare(eventAggregator);
            LED.Init();
        }


        public void OnInitialized(IContainerProvider containerProvider)
        {

        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {

        }
    }
}