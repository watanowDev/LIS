using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using WATA.LIS.Core.Events.VisionCam;
using WATA.LIS.Core.Model.VisionCam;
using System.IO;
using System.Windows.Controls;
using System.Collections.ObjectModel;
using WATA.LIS.Core.Common;
using System.Windows;

namespace WATA.LIS.VISION.CAM.ViewModels
{
    public class CamViewModel : BindableBase
    {

        private readonly IEventAggregator _eventAggregator;

        private BitmapImage _currentFrame;
        public BitmapImage CurrentFrame { get { return _currentFrame; } set { SetProperty(ref _currentFrame, value); } }

        public ObservableCollection<Log> ListVisionCamLog { get; set; }


        public CamViewModel(IEventAggregator eventAggregator)
        {
            ListVisionCamLog = Tools.logInfo.ListVisionCamLog;
            Tools.Log($"Init CamViewModel", Tools.ELogType.VisionCamLog);

            //_eventAggregator = eventAggregator;
            //_eventAggregator.GetEvent<HikVisionEvent>().Subscribe(OnVisionCamStreaming, ThreadOption.BackgroundThread, true);
        }

        private void OnVisionCamStreaming(VisionCamModel model)
        {
            if (model?.FRAME != null)
            {
                // UI 스레드에서 CurrentFrame 속성을 업데이트
                Application.Current.Dispatcher.Invoke(() => {
                    CurrentFrame = ConvertToBitmapImage(model.FRAME);
                });
            }
        }

        private BitmapImage ConvertToBitmapImage(byte[] frame)
        {
            using (var stream = new MemoryStream(frame))
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = stream;
                bitmap.EndInit();
                bitmap.Freeze(); // BitmapImage를 다른 스레드에서 사용할 수 있도록 고정
                return bitmap;
            };
        }
    }
}
