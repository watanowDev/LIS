using BlinkStickDotNet;
using ControlzEx.Standard;
using Prism.Events;
using System;
using System.Threading;
using WATA.LIS.Core.Common;
using WATA.LIS.Core.Events.BackEnd;
using WATA.LIS.Core.Events.StatusLED;
using WATA.LIS.Core.Interfaces;
using WATA.LIS.Core.Model.ErrorCheck;
using WATA.LIS.Core.Model.SystemConfig;

namespace WATA.LIS.INDICATOR.LED.StatusLED
{
    public class Patlite_LED_Buzzer
    {
        
        private readonly IEventAggregator _eventAggregator;
        private Led_Buzzer_ConfigModel _ledBuzzer;
        private int m_volume = 0;

        public Patlite_LED_Buzzer(IEventAggregator eventAggregator, ILedBuzzertModel ledBuzzer)
        {
           _eventAggregator = eventAggregator;

            _ledBuzzer = (Led_Buzzer_ConfigModel)ledBuzzer;

            m_volume =  _ledBuzzer.volume;


        }



        public void Init()
        {
            try
            {

                int result = NeUsbController.NeUsbController.NE_OpenDevice();

                _eventAggregator.GetEvent<Pattlite_StatusLED_Event>().Subscribe(OnLEDEvent, ThreadOption.BackgroundThread, true);
            }
            catch
            {

                Tools.Log($"Patlite Led Exception", Tools.ELogType.SystemLog);

            }
          
            NeUsbController.NeUsbController.NE_SetLight(NeUsbController.LEDColors.Green, NeUsbController.LEDPatterns.Continuous);
            NeUsbController.NeUsbController.NE_SetBuz(NeUsbController.BuzzerPatterns.Pattern6, m_volume, 1);
        }


        public void OnLEDEvent(Pattlite_LED_Buzzer_Model status)
        {
            SetBuzzer(status.BuzzerPattern, status.BuzzerCount);
            SetLight(status.LED_Color, status.LED_Pattern);
        }


        private void SetLight(eLEDColors color, eLEDPatterns pattern)
        {

           

            try
            {

                NeUsbController.NeUsbController.NE_SetLight((NeUsbController.LEDColors)color, (NeUsbController.LEDPatterns)pattern);
            }
            catch
            {

                Tools.Log($"Patlite Led SetLight exception", Tools.ELogType.SystemLog);



            }

        }

        bool State = false;


        private bool GetBuzzerPlaying()
        {
            bool BozzerState;
            bool LedState;
            bool TouchState;



            NeUsbController.NeUsbController.NE_GetDeviceState(out BozzerState, out LedState, out TouchState);

            Tools.Log($"BozzerState : {BozzerState } LedState : {LedState} TouchState : {TouchState}", Tools.ELogType.WeightLog);
            return BozzerState;
        }

        private void SetBuzzer(eBuzzerPatterns Pattern, int count)


        {


            try
            {
                
                 


                NeUsbController.NeUsbController.NE_SetBuz((NeUsbController.BuzzerPatterns)Pattern, m_volume, count);

                

            }
            catch
            {

                Tools.Log($"Patlite Led SetBuzzer exception", Tools.ELogType.SystemLog);
            }
        }
    }
}
