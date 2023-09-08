using BlinkStickDotNet;
using Prism.Events;
using System;
using WATA.LIS.Core.Common;
using WATA.LIS.Core.Events.BackEnd;
using WATA.LIS.Core.Events.StatusLED;

namespace WATA.LIS.INDICATOR.LED.StatusLED
{
    public class BlinkStickSquare
    {
        BlinkStick led;

        private readonly IEventAggregator _eventAggregator;

        public BlinkStickSquare(IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;
        }


        public void Init()
        {
            try
            {


                led = BlinkStick.FindFirst();

                if(led != null)
                {
                    led.OpenDevice();
                    _eventAggregator.GetEvent<StatusLED_Event>().Subscribe(OnLEDEvent, ThreadOption.BackgroundThread, true);
                }

     

            }
            catch
            {


            }
    
        }

        public void OnLEDEvent(string status)
        {

            if (led != null)
            {
                Tools.Log($"SetLED {status}", Tools.ELogType.SystemLog);
                led.SetColor(status);
            }
        }
   
    }
}
