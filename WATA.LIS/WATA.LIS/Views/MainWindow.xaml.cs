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
            // 작업표시줄 위에 창을 표시하도록 설정
            this.WindowState = WindowState.Normal;
            this.ResizeMode = ResizeMode.CanResize;
            this.Topmost = true;

            // 화면 크기로 창 크기 설정
            var screen = System.Windows.Forms.Screen.FromHandle(new System.Windows.Interop.WindowInteropHelper(this).Handle);
            this.Left = screen.WorkingArea.Left;
            this.Top = screen.WorkingArea.Top;
            this.Width = screen.WorkingArea.Width;
            this.Height = screen.WorkingArea.Height;
            this.WindowState = WindowState.Maximized;
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // 닫기 버튼을 클릭했을 때 수행할 동작
            if (MessageBox.Show("Do you want to exit the program?", "Program", MessageBoxButton.YesNo, MessageBoxImage.Asterisk) == MessageBoxResult.Yes)
            {
                Environment.Exit(0);
            }
            else
            {
                // 창 닫기를 취소합니다.
                e.Cancel = true;
            }
        }
    }
}