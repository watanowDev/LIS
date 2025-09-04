using Prism.Regions;
using System.Windows;
using System.Windows.Controls;
using WATA.LIS.Core;

namespace WATA.LIS.Main.Views
{
    public partial class NavigationBarUI : UserControl
    {
        public NavigationBarUI()
        {
            InitializeComponent();
        }

        private void NavChecked(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Primitives.ToggleButton b && b.Tag is string target && !string.IsNullOrWhiteSpace(target))
            {
                var rm = Prism.Ioc.ContainerLocator.Container.Resolve(typeof(IRegionManager)) as IRegionManager;
                rm?.RequestNavigate(RegionNames.Content_Main, target);
            }
        }
    }
}
