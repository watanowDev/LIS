using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Prism.Regions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using WATA.LIS.Core.Common;

namespace WATA.LIS.Main.ViewModels
{
    public class TopBarUIViewModel : BindableBase
    {
        

        private string _VersionContext;
        public string VersionContext { get { return _VersionContext; } set { SetProperty(ref _VersionContext, value); } }


        private string _Date;
        public string Date { get { return _Date; } set { SetProperty(ref _Date, value); } }

        private string _Time;
        public string Time { get { return _Time; } set { SetProperty(ref _Time, value); } }



        public DelegateCommand<string> ButtonFunc { get; set; }

        public TopBarUIViewModel(IRegionManager regionManager, IEventAggregator eventAggregator)
        {
            VersionContext = "(LIS)" + GlobalValue.SystemVersion;

            ButtonFunc = new DelegateCommand<string>(ButtonFuncClick);


            DispatcherTimer DateTimer = new DispatcherTimer();
            DateTimer.Interval = new TimeSpan(0, 0, 0, 0, 1000);
            DateTimer.Tick += new EventHandler(Timer);
            DateTimer.Start();


        }

        private void Timer(object sender, EventArgs e)
        {
            Date = DateTime.Now.ToString("yyyy-MM-dd");
            Time = DateTime.Now.ToString("HH:mm:ss");
        }


        private void ClearProcess()
        {
            Process[] processList = Process.GetProcessesByName("WATA.LIS.WPS");
            for (int i = processList.Length - 1; i >= 0; i--)
            {
                // processList[i].CloseMainWindow();
                processList[i].Kill();
                processList[i].Close();
            }

            Process[] processList_vision = Process.GetProcessesByName("vision_forklift");
            for (int i = processList_vision.Length - 1; i >= 0; i--)
            {
                // processList[i].CloseMainWindow();
                processList_vision[i].Kill();
                processList_vision[i].Close();
            }



        }

        private void ButtonFuncClick(string command)
        {
            try
            {
                if (command == null) return;
                switch (command)
                {
                    case "Exit":

                        if (MessageBox.Show(" Do you want to exit the program? ", "Program", MessageBoxButton.YesNo, MessageBoxImage.Asterisk) == MessageBoxResult.Yes)
                        {

                            ClearProcess();

                            Environment.Exit(0);
                        }

                        break;
                    default:
                        break;
                }
            }
            catch (Exception ex)
            {
         
            }
        }
    }
}
