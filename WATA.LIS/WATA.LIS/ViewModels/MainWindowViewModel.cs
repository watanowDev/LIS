using Prism.Mvvm;
using WATA.LIS.Core.Common;
using WATA.LIS.Core.Interfaces;
using WATA.LIS.Core.Model.SystemConfig;
using WATA.LIS.Core.Services;

namespace WATA.LIS.ViewModels
{
    public class MainWindowViewModel : BindableBase
    {
        private string _title = "WATA";
        public string Title
        {
            get { return _title; }
            set { SetProperty(ref _title, value); }
        }

        private string _MainRegionName;
        public string MainRegionName { get { return _MainRegionName; } set { SetProperty(ref _MainRegionName, value); } }



        public MainWindowViewModel(IMainModel main )
        {
            GlobalValue.SystemVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
            MainConfigModel mainobj = (MainConfigModel)main;

            if(mainobj.device_type == "DPS")
            {
                MainRegionName = "Content_DPS";
            }
            else
            {
                MainRegionName = "Content_Main";
            }           
        }
    }
}
