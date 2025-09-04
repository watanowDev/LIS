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
            // 닫기 버튼(X) 클릭 시에도 종료 확인 모달을 표시
            var result = MessageBox.Show(" Do you want to exit the program? ", "Program", MessageBoxButton.YesNo, MessageBoxImage.Asterisk);
            if (result != MessageBoxResult.Yes)
            {
                e.Cancel = true; // 사용자가 No를 선택하면 닫기 취소
                return;
            }

            // 확인 시 기존 동작 유지
            Environment.Exit(0);
        }
    }
}