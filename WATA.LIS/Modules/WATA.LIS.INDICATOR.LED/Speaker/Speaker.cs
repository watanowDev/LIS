using BlinkStickDotNet;
using ControlzEx.Standard;
using javax.sound.sampled.spi;
using Prism.Events;
using System;
using System.Threading;
using System.Windows.Media;
using WATA.LIS.Core.Common;
using WATA.LIS.Core.Events.BackEnd;
using WATA.LIS.Core.Events.StatusLED;
using WATA.LIS.Core.Interfaces;
using WATA.LIS.Core.Model.ErrorCheck;
using WATA.LIS.Core.Model.SystemConfig;

namespace WATA.LIS.INDICATOR.LED.StatusLED
{
    public class Speaker
    {
        
        private readonly IEventAggregator _eventAggregator;
        private Led_Buzzer_ConfigModel _ledBuzzer;
        private int m_volume = 0;




        public Speaker(IEventAggregator eventAggregator, ILedBuzzertModel ledBuzzer)
        {
           _eventAggregator = eventAggregator;

            _ledBuzzer = (Led_Buzzer_ConfigModel)ledBuzzer;

        }

        public void Init()
        {
            m_volume = _ledBuzzer.volume;
            _eventAggregator.GetEvent<SpeakerInfoEvent>().Subscribe(OnPlayEvent, ThreadOption.BackgroundThread, true);
        }


        public void OnPlayEvent(ePlayInfoSpeaker speaker)
        {
             if(_ledBuzzer.InfoLanguage == "JP")
            {
                if(speaker == ePlayInfoSpeaker.size_check_start)
                {
                    PlaySound("jp_size_check_start.mp3");
                }
                else if (speaker == ePlayInfoSpeaker.size_check_complete)
                {
                    PlaySound("jp_size_check_complete.mp3");
                }
                else if (speaker == ePlayInfoSpeaker.weight_check_start)
                {
                    PlaySound("jp_weight_check_start.mp3");
                }
                else if (speaker == ePlayInfoSpeaker.weight_check_complete)
                {
                    PlaySound("jp_weight_check_complete.mp3");
                }
                else if (speaker == ePlayInfoSpeaker.weight_check_error)
                {
                    PlaySound("jp_weight_error.mp3");
                }
                else if (speaker == ePlayInfoSpeaker.qr_check_error)
                {
                    PlaySound("jp_qr_error.mp3");
                }
                else if (speaker == ePlayInfoSpeaker.dummy)
                {
                    PlaySound("dummy.mp3");
                }


            }
            else if (_ledBuzzer.InfoLanguage == "KR")
            {
                if (speaker == ePlayInfoSpeaker.dummy)
                {
                    PlaySound("dummy.mp3");
                }
                else if (speaker == ePlayInfoSpeaker.stop)
                {
                    PlaySound("kr_stop.mp3");
                }
                else if (speaker == ePlayInfoSpeaker.measure_complete)
                {
                    PlaySound("kr_measure_complete.mp3");
                }
                else if (speaker == ePlayInfoSpeaker.qr_check_start)
                {
                    PlaySound("kr_qr_check_start.mp3");
                }
                else if (speaker == ePlayInfoSpeaker.qr_check_complete)
                {
                    PlaySound("kr_qr_check_complete.mp3");
                }
                else if (speaker == ePlayInfoSpeaker.qr_check_error)
                {
                    PlaySound("kr_qr_check_error.mp3");
                }
                else if (speaker == ePlayInfoSpeaker.weight_check_start)
                {
                    PlaySound("kr_weight_check_start.mp3");
                }
                else if (speaker == ePlayInfoSpeaker.weight_check_complete)
                {
                    PlaySound("kr_weight_check_complete.mp3");
                }
                else if (speaker == ePlayInfoSpeaker.weight_check_error)
                {
                    PlaySound("kr_weight_check_error.mp3");
                }
                else if (speaker == ePlayInfoSpeaker.size_check_start)
                {
                    PlaySound("kr_size_check_start.mp3");
                }
                else if (speaker == ePlayInfoSpeaker.size_check_start_please_stop)
                {
                    PlaySound("kr_size_check_start_please_stop.mp3");
                }
                else if (speaker == ePlayInfoSpeaker.size_check_complete)
                {
                    PlaySound("kr_size_check_complete.mp3");
                }
                else if (speaker == ePlayInfoSpeaker.size_check_complete_please_pickup)
                {
                    PlaySound("kr_size_check_complete_please_pickup.mp3");
                }
                else if (speaker == ePlayInfoSpeaker.size_check_error)
                {
                    PlaySound("kr_size_check_error.mp3");
                }
                else if (speaker == ePlayInfoSpeaker.weight_size_check_start)
                {
                    PlaySound("kr_weight_size_check_start.mp3");
                }
                else if (speaker == ePlayInfoSpeaker.weight_size_check_complete)
                {
                    PlaySound("kr_weight_size_check_complete.mp3");
                }
                else if (speaker == ePlayInfoSpeaker.weight_size_check_error)
                {
                    PlaySound("kr_weight_size_check_error.mp3");
                }
                else if (speaker == ePlayInfoSpeaker.set_item)
                {
                    PlaySound("kr_set_item.mp3");
                }
                else if (speaker == ePlayInfoSpeaker.clear_item)
                {
                    PlaySound("kr_clear_item.mp3");
                }
                else if (speaker == ePlayInfoSpeaker.device_error_clear)
                {
                    PlaySound("kr_device_error_clear.mp3");
                }
                else if (speaker == ePlayInfoSpeaker.device_error_weight)
                {
                    PlaySound("kr_device_error_weight.mp3");
                }
                else if (speaker == ePlayInfoSpeaker.device_error_distance)
                {
                    PlaySound("kr_device_error_distance.mp3");
                }
                else if (speaker == ePlayInfoSpeaker.device_error_visoncam)
                {
                    PlaySound("kr_device_error_visoncam.mp3");
                }
                else if (speaker == ePlayInfoSpeaker.device_error_lidar3d)
                {
                    PlaySound("kr_device_error_lidar3d.mp3");
                }
                else if (speaker == ePlayInfoSpeaker.device_error_indicator)
                {
                    PlaySound("kr_device_error_indicator.mp3");
                }
                else if (speaker == ePlayInfoSpeaker.register_item)
                {
                    PlaySound("kr_register_item.mp3");
                }

            }
            else //en
            {
                if (speaker == ePlayInfoSpeaker.dummy)
                {
                    PlaySound("dummy.mp3");
                }
                else if (speaker == ePlayInfoSpeaker.stop)
                {
                    PlaySound("en_stop.mp3");
                }
                else if (speaker == ePlayInfoSpeaker.measure_complete)
                {
                    PlaySound("en_measure_complete.mp3");
                }
                else if (speaker == ePlayInfoSpeaker.qr_check_start)
                {
                    PlaySound("en_qr_check_start.mp3");
                }
                else if (speaker == ePlayInfoSpeaker.qr_check_complete)
                {
                    PlaySound("en_qr_check_complete.mp3");
                }
                else if (speaker == ePlayInfoSpeaker.qr_check_error)
                {
                    PlaySound("en_qr_check_error.mp3");
                }
                else if (speaker == ePlayInfoSpeaker.weight_check_start)
                {
                    PlaySound("en_weight_check_start.mp3");
                }
                else if (speaker == ePlayInfoSpeaker.weight_check_complete)
                {
                    PlaySound("en_weight_check_complete.mp3");
                }
                else if (speaker == ePlayInfoSpeaker.weight_check_error)
                {
                    PlaySound("en_weight_check_error.mp3");
                }
                else if (speaker == ePlayInfoSpeaker.size_check_start)
                {
                    PlaySound("en_size_check_start.mp3");
                }
                else if (speaker == ePlayInfoSpeaker.size_check_start_please_stop)
                {
                    PlaySound("en_size_check_start_please_stop.mp3");
                }
                else if (speaker == ePlayInfoSpeaker.size_check_complete)
                {
                    PlaySound("en_size_check_complete.mp3");
                }
                else if (speaker == ePlayInfoSpeaker.size_check_complete_please_pickup)
                {
                    PlaySound("en_size_check_complete_please_pickup.mp3");
                }
                else if (speaker == ePlayInfoSpeaker.size_check_error)
                {
                    PlaySound("en_size_check_error.mp3");
                }
                else if (speaker == ePlayInfoSpeaker.weight_size_check_start)
                {
                    PlaySound("en_weight_size_check_start.mp3");
                }
                else if (speaker == ePlayInfoSpeaker.weight_size_check_complete)
                {
                    PlaySound("en_weight_size_check_complete.mp3");
                }
                else if (speaker == ePlayInfoSpeaker.weight_size_check_error)
                {
                    PlaySound("en_weight_size_check_error.mp3");
                }
                else if (speaker == ePlayInfoSpeaker.set_item)
                {
                    PlaySound("en_set_item.mp3");
                }
                else if (speaker == ePlayInfoSpeaker.clear_item)
                {
                    PlaySound("en_clear_item.mp3");
                }
                else if (speaker == ePlayInfoSpeaker.device_error_clear)
                {
                    PlaySound("en_device_error_clear.mp3");
                }
                else if (speaker == ePlayInfoSpeaker.device_error_weight)
                {
                    PlaySound("en_device_error_weight.mp3");
                }
                else if (speaker == ePlayInfoSpeaker.device_error_distance)
                {
                    PlaySound("en_device_error_distance.mp3");
                }
                else if (speaker == ePlayInfoSpeaker.device_error_visoncam)
                {
                    PlaySound("en_device_error_visoncam.mp3");
                }
                else if (speaker == ePlayInfoSpeaker.device_error_lidar3d)
                {
                    PlaySound("en_device_error_lidar3d.mp3");
                }
                else if (speaker == ePlayInfoSpeaker.device_error_indicator)
                {
                    PlaySound("en_device_error_indicator.mp3");
                }
                else if (speaker == ePlayInfoSpeaker.register_item)
                {
                    PlaySound("en_register_item.mp3");
                }
            }
        }

        public void PlaySound(string FileName)
        {
            try
            {
                //Thread.Sleep(2000);
                MediaPlayer mMediaPlayer = new MediaPlayer();
                mMediaPlayer.Open(new Uri("WaveResource\\" + FileName, UriKind.RelativeOrAbsolute));
                mMediaPlayer.Play();
                Thread.Sleep(300);
                //Tools.Log($"PlaySound {FileName}", Tools.ELogType.ActionLog);
                //mMediaPlayer.Close();

            }
            catch
            {

                Tools.Log($"PlaySound Exception", Tools.ELogType.ActionLog);

            }


            
        }
    }
}
