using System.Windows;
using WATA.LIS.Core;
using System;
using System.Diagnostics;

namespace WATA.LIS.Views
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            this.Closing += MainWindow_Closing;

            // 창을 전체화면으로 설정
            this.Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // 최대화로 시작하며 작업표시줄은 침범하지 않음
            this.WindowState = WindowState.Maximized;
            this.ResizeMode = ResizeMode.CanResize;
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // 닫기 버튼을 클릭했을 때 수행할 동작
            Environment.Exit(0);
        }
    }
}