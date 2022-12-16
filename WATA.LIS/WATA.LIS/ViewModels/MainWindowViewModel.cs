using Prism.Mvvm;
using WATA.LIS.Core.Common;

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

        public MainWindowViewModel()
        {
            GlobalValue.SystemVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();

        }
    }
}
