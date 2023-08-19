using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Prism.Regions;
using System;
using System.Collections.Generic;
using System.Linq;
using WATA.LIS.Core;

namespace WATA.LIS.Main.ViewModels
{
    public class BottomBarUIViewModel : BindableBase
    {

        private readonly IRegionManager _regionManager;
        private readonly IEventAggregator _eventAggregator;

        public DelegateCommand<string> MenuSelectButton { get; private set; }
        
        private bool _MainChecked;
        public bool MainChecked { get { return _MainChecked; } set { SetProperty(ref _MainChecked, value); } }

        private bool _RFIDChecked;
        public bool RFIDChecked { get { return _RFIDChecked; } set { SetProperty(ref _RFIDChecked, value); } }


        private bool _WeightChecked;
        public bool WeightChecked { get { return _WeightChecked; } set { SetProperty(ref _WeightChecked, value); } }



        private bool _DistanceChecked;
        public bool DistanceChecked { get { return _DistanceChecked; } set { SetProperty(ref _DistanceChecked, value); } }

        private bool _CameraChecked;
        public bool CameraChecked { get { return _CameraChecked; } set { SetProperty(ref _CameraChecked, value); } }


        public BottomBarUIViewModel(IRegionManager regionManager, IEventAggregator eventAggregator)
        {
            _regionManager = regionManager;
            _eventAggregator = eventAggregator;

            MenuSelectButton = new DelegateCommand<string>(MenuSelect);
        }

        private void MenuSelect(string command)
        {
            MainChecked = false;
            RFIDChecked = false;
            DistanceChecked = false;
            CameraChecked = false;
            WeightChecked = false;



            if (command != null)
            {
              

                switch (command)
                {
                    case "Content_Main":
                        MainChecked = true;
                        break;
                    case "Content_RFID":
                        RFIDChecked = true;
                        break;
                    case "Content_Distance":
                        DistanceChecked = true;
                        break;
                    case "Content_Camera":
                        CameraChecked = true;
                        break;
                    case "Content_Weight":
                        WeightChecked = true;
                        break;

                }
                _regionManager.RequestNavigate(RegionNames.Content_Main, command);
            }

        }

    }
}
