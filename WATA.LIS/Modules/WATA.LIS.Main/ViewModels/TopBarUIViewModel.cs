using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Prism.Regions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using WATA.LIS.Core.Common;

namespace WATA.LIS.Main.ViewModels
{
    public class TopBarUIViewModel : BindableBase
    {
        public DelegateCommand<string> ButtonFunc { get; set; }

        public TopBarUIViewModel(IRegionManager regionManager, IEventAggregator eventAggregator)
        {
            ButtonFunc = new DelegateCommand<string>(ButtonFuncClick);
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

                            Process[] processList = Process.GetProcessesByName("WATA.LIS.WPS");
                            for (int i = processList.Length - 1; i >= 0; i--)
                            {
                                // processList[i].CloseMainWindow();
                                processList[i].Kill();
                                processList[i].Close();
                            }


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
