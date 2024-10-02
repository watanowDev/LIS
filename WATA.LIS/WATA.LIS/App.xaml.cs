
using Prism.Ioc;
using Prism.Modularity;
using System;
using System.Windows;
using WATA.LIS.Core.Interfaces;
using WATA.LIS.Core.Model.RFID;
using WATA.LIS.Core.Model.SystemConfig;
using WATA.LIS.Core.Parser;
using WATA.LIS.Core.Services;
using WATA.LIS.IF.BE;
using WATA.LIS.IF.DPS;
using WATA.LIS.INDICATOR.DISPLAY;
using WATA.LIS.INDICATOR.LED;
using WATA.LIS.Main;
using WATA.LIS.SENSOR.Distance;
using WATA.LIS.SENSOR.NAV;
using WATA.LIS.SENSOR.UHF_RFID;
using WATA.LIS.SENSOR.WEIGHT;
using WATA.LIS.Views;
using WATA.LIS.VISION.Camera;
using WATA.LIS.VISION.CAM;

namespace WATA.LIS
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App
    {
        protected override Window CreateShell()
        {
            return Container.Resolve<MainWindow>();
        }


        protected override void RegisterTypes(IContainerRegistry containerRegistry)
        {

            var parser = new SystemJsonConfigParser();

            (IWeightModel weight, IDistanceModel distance, IVisionModel vision, IRFIDModel rfid, IMainModel main, ILedBuzzertModel LedBuzzer, IDPSModel dpsmodel, INAVModel navmodel, IQRCameraModel qrcameramodel) = parser.LoadJsonfile();

            containerRegistry.RegisterSingleton<IWeightModel>(x => weight);
            containerRegistry.RegisterSingleton<IDistanceModel>(x => distance);
            containerRegistry.RegisterSingleton<IVisionModel>(x => vision);
            containerRegistry.RegisterSingleton<IRFIDModel>(x => rfid);
            containerRegistry.RegisterSingleton<IMainModel>(x => main);
            containerRegistry.RegisterSingleton<ILedBuzzertModel>(x => LedBuzzer);
            containerRegistry.RegisterSingleton<IDPSModel>(x => dpsmodel);
            containerRegistry.RegisterSingleton<INAVModel>(x => navmodel);
            containerRegistry.RegisterSingleton<IQRCameraModel>(x => qrcameramodel);

            if (!containerRegistry.IsRegistered<IWeightModel>())
                containerRegistry.RegisterSingleton<IWeightModel, WeightConfigModel>();

            if (!containerRegistry.IsRegistered<IDistanceModel>())
                containerRegistry.RegisterSingleton<IDistanceModel, DistanceConfigModel>();

            if (!containerRegistry.IsRegistered<IVisionModel>())
                containerRegistry.RegisterSingleton<IVisionModel, VisionConfigModel>();

            if (!containerRegistry.IsRegistered<IRFIDModel>())
                containerRegistry.RegisterSingleton<IRFIDModel, RFIDConfigModel>();

            if (!containerRegistry.IsRegistered<IMainModel>())
                containerRegistry.RegisterSingleton<IMainModel, MainConfigModel>();

            if (!containerRegistry.IsRegistered<ILedBuzzertModel>())
                containerRegistry.RegisterSingleton<ILedBuzzertModel, Led_Buzzer_ConfigModel>();

            if (!containerRegistry.IsRegistered<IDPSModel>())
                containerRegistry.RegisterSingleton<IDPSModel, DPSConfigModel>();

            if (!containerRegistry.IsRegistered<INAVModel>())
                containerRegistry.RegisterSingleton<INAVModel, NAVConfigModel>();

            if (!containerRegistry.IsRegistered<IQRCameraModel>())
                containerRegistry.RegisterSingleton<IQRCameraModel, VisionCamConfigModel>();


            MainConfigModel mainobj = (MainConfigModel)main;

            if (mainobj.device_type == "fork_lift_v1")
            {
                containerRegistry.RegisterSingleton<IStatusService, StatusService_V1>();//현재 안씀 FH-920 RF수신기 2023.09.27 현재는 쓰지 않음
            }
            else if (mainobj.device_type == "pantos")// 판토스향
            {
                containerRegistry.RegisterSingleton<IStatusService, StatusService_Pantos>();//현재 지게차용  Apulse RF수신기
            }
            else if (mainobj.device_type == "calt")//칼트향
            {
                containerRegistry.RegisterSingleton<IStatusService, StatusService_CALT>();//현재 지게차용  Apulse RF수신기

            }
            else if (mainobj.device_type == "gate_checker")//창고방 Gate Sender 현재는 안씀
            {
                containerRegistry.RegisterSingleton<IStatusService, StatusService_GateChecker>();
            }
            else if (mainobj.device_type == "DPS")//DPS DPS 컨트롤 테스트때 사용
            {
                containerRegistry.RegisterSingleton<IStatusService, StatusService_DPS>();
            }
            else if (mainobj.device_type == "NXDPOC")//NXD POC 
            {
                containerRegistry.RegisterSingleton<IStatusService, StatusService_NXDPOC>();// 니뽄 익스프레스 POC용도
            }
            else if (mainobj.device_type == "WIS_KINTEX")//
            {
                containerRegistry.RegisterSingleton<IStatusService, StatusService_WIS_KINTEX>();// 국내전시회 3x3 선반용
            }
            else if (mainobj.device_type == "CTR")//CTR POC
            {
                containerRegistry.RegisterSingleton<IStatusService, StatusService_CTR>();//현재 지게차용  Apulse RF수신기
            }
            else if (mainobj.device_type == "Singapore")//Singapore POC
            {
                containerRegistry.RegisterSingleton<IStatusService, StatusService_Singapore>();//싱가포르 POC용 원민섭 주임 신규개발
            }
        }

        protected override void ConfigureModuleCatalog(IModuleCatalog moduleCatalog)
        {
            moduleCatalog.AddModule<LEDModule>();
            moduleCatalog.AddModule<DistanceModule>();
            moduleCatalog.AddModule<WEIGHTModule>();
            moduleCatalog.AddModule<BEModule>();
            moduleCatalog.AddModule<CameraModule>();
            moduleCatalog.AddModule<MainModule>();
            moduleCatalog.AddModule<UHF_RFIDModule>();
            moduleCatalog.AddModule<DISPLAYModule>();
            moduleCatalog.AddModule<DPSModule>();
            moduleCatalog.AddModule<NAVModule>();
            moduleCatalog.AddModule<CAMModule>();
        }
    }
}
