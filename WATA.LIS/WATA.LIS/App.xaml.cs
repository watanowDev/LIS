
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
using WATA.LIS.Main;
using WATA.LIS.SENSOR.Distance;
using WATA.LIS.SENSOR.UHF_RFID;
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

            ( IDistanceModel distance, IVisionModel vision , IRFIDModel rfid ,IMainModel main) = parser.LoadJsonfile();

            containerRegistry.RegisterSingleton<IDistanceModel>(x => distance);
            containerRegistry.RegisterSingleton<IVisionModel>(x => vision);
            containerRegistry.RegisterSingleton<IRFIDModel>(x => rfid);
            containerRegistry.RegisterSingleton<IMainModel>(x => main);



            if (!containerRegistry.IsRegistered<IDistanceModel>())
                containerRegistry.RegisterSingleton<IDistanceModel, DistanceConfigModel>();

            if (!containerRegistry.IsRegistered<IVisionModel>())
                containerRegistry.RegisterSingleton<IVisionModel, VisionConfigModel>();

            if (!containerRegistry.IsRegistered<IRFIDModel>())
                containerRegistry.RegisterSingleton<IRFIDModel, RFIDConfigModel>();

               if (!containerRegistry.IsRegistered<IRFIDModel>())
                containerRegistry.RegisterSingleton<IMainModel, MainConfigModel>();



            //containerRegistry.RegisterSingleton<IStatusService, StatusService_V1>();
            containerRegistry.RegisterSingleton<IStatusService, StatusService_V2>();

        }

        protected override void ConfigureModuleCatalog(IModuleCatalog moduleCatalog)
        {
            moduleCatalog.AddModule<DistanceModule>();
            moduleCatalog.AddModule<BEModule>();
            moduleCatalog.AddModule<CameraModule>();
            moduleCatalog.AddModule<MainModule>();
            moduleCatalog.AddModule<UHF_RFIDModule>();
        }
    }
}
