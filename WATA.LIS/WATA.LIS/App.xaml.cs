
using Prism.Ioc;
using Prism.Modularity;
using System.Windows;
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
            containerRegistry.RegisterSingleton<IStatusService, StatusService>();

        }

        protected override void ConfigureModuleCatalog(IModuleCatalog moduleCatalog)
        {
            moduleCatalog.AddModule<DistanceModule>();
            moduleCatalog.AddModule<UHF_RFIDModule>();
            moduleCatalog.AddModule<BEModule>();
            moduleCatalog.AddModule<CameraModule>();
            moduleCatalog.AddModule<MainModule>();
        }
    }
}
