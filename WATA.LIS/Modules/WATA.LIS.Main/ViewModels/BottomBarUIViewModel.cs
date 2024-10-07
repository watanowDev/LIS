using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Prism.Regions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using WATA.LIS.Core;
using WATA.LIS.Core.Interfaces;
using WATA.LIS.Core.Model.SystemConfig;

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


        private bool _BackEndChecked;
        public bool BackEndChecked { get { return _BackEndChecked; } set { SetProperty(ref _BackEndChecked, value); } }


        private bool _IndicatorChecked;
        public bool IndicatorChecked { get { return _IndicatorChecked; } set { SetProperty(ref _IndicatorChecked, value); } }


        private bool _DPSChecked;
        public bool DPSChecked { get { return _DPSChecked; } set { SetProperty(ref _DPSChecked, value); } }


        private bool _VisionCamChecked;
        public bool VisionCamChecked { get { return _VisionCamChecked; } set { SetProperty(ref _VisionCamChecked, value); } }


        private bool _LiDAR2DChecked;
        public bool LiDAR2DChecked { get { return _LiDAR2DChecked; } set { SetProperty(ref _LiDAR2DChecked, value); } }


        private bool _LiDAR3DChecked;
        public bool LiDAR3DChecked { get { return _LiDAR3DChecked; } set { SetProperty(ref _LiDAR3DChecked, value); } }







        public Visibility ForkLiftVisible { get; set; } = Visibility.Visible;
        public Visibility DPSVisible { get; set; } = Visibility.Visible;

        public BottomBarUIViewModel(IRegionManager regionManager, IEventAggregator eventAggregator, IMainModel main)
        {
            _regionManager = regionManager;
            _eventAggregator = eventAggregator;

            MenuSelectButton = new DelegateCommand<string>(MenuSelect);
            MainConfigModel mainobj = (MainConfigModel)main;
            if (mainobj.device_type == "DPS")
            {
                ForkLiftVisible = Visibility.Hidden;
                DPSVisible = Visibility.Visible;
            }
            else
            {
                ForkLiftVisible = Visibility.Visible;
                DPSVisible = Visibility.Hidden;
            }
        }

        private void MenuSelect(string command)
        {
            MainChecked = false;
            RFIDChecked = false;
            WeightChecked = false;
            DistanceChecked = false;
            CameraChecked = false;
            BackEndChecked = false;
            IndicatorChecked = false;
            DPSChecked = false;
            VisionCamChecked = false;
            LiDAR2DChecked = false;
            LiDAR3DChecked = false;

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

                    case "Content_Weight":
                        WeightChecked = true;
                        break;

                    case "Content_Distance":
                        DistanceChecked = true;
                        break;
                    
                    case "Content_Camera":
                        CameraChecked = true;
                        break;

                    case "Content_BackEnd":
                        BackEndChecked = true;
                        break;

                    case "Content_Indicator":
                        IndicatorChecked = true;
                        break;

                    case "Content_DPS":
                        DPSChecked = true;
                        break;

                    case "Content_VisionCam":
                        VisionCamChecked = true;
                        break;

                    case "Content_LiDAR2D":
                        LiDAR2DChecked = true;
                        break;

                    case "Content_LiDAR3D":
                        LiDAR3DChecked = true;
                        break;
                }
                _regionManager.RequestNavigate(RegionNames.Content_Main, command);
            }
        }
    }
}
