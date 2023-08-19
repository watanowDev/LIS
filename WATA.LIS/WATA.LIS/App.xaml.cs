
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
using WATA.LIS.INDICATOR.LED;
using WATA.LIS.Main;
using WATA.LIS.SENSOR.Distance;
using WATA.LIS.SENSOR.UHF_RFID;
using WATA.LIS.SENSOR.WEIGHT;
using WATA.LIS.Views;
using WATA.LIS.VISION.Camera;

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

            (IWeightModel  weight, IDistanceModel distance, IVisionModel vision , IRFIDModel rfid ,IMainModel main) = parser.LoadJsonfile();

            containerRegistry.RegisterSingleton<IWeightModel>(x => weight);
            containerRegistry.RegisterSingleton<IDistanceModel>(x => distance);
            containerRegistry.RegisterSingleton<IVisionModel>(x => vision);
            containerRegistry.RegisterSingleton<IRFIDModel>(x => rfid);
            containerRegistry.RegisterSingleton<IMainModel>(x => main);



            if (!containerRegistry.IsRegistered<IWeightModel>())
                containerRegistry.RegisterSingleton<IWeightModel, WeightConfigModel>();


            if (!containerRegistry.IsRegistered<IDistanceModel>())
                containerRegistry.RegisterSingleton<IDistanceModel, DistanceConfigModel>();

            if (!containerRegistry.IsRegistered<IVisionModel>())
                containerRegistry.RegisterSingleton<IVisionModel, VisionConfigModel>();

            if (!containerRegistry.IsRegistered<IRFIDModel>())
                containerRegistry.RegisterSingleton<IRFIDModel, RFIDConfigModel>();

               if (!containerRegistry.IsRegistered<IRFIDModel>())
                containerRegistry.RegisterSingleton<IMainModel, MainConfigModel>();


            MainConfigModel mainobj = (MainConfigModel)main;

            if (mainobj.device_type == "fork_lift_v1") 
            {
                containerRegistry.RegisterSingleton<IStatusService, StatusService_V1>();//현재 안씀 FH-920 RF수신기
            }
            else if(mainobj.device_type == "fork_lift_v2") 
            {
                containerRegistry.RegisterSingleton<IStatusService, StatusService_V2>();//현재 지게차용  Apulse RF수신기
            }
            else if(mainobj.device_type == "gate_checker")//창고방 Gate Sender
            {
                containerRegistry.RegisterSingleton<IStatusService, StatusService_GateChecker>();
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
           

        }
    }
}
