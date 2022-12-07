using NetMQ;
using NetMQ.Sockets;
using RFID.Service;
using RFID.Service.IInterface.BLE;
using RFID.Service.IInterface.BLE.IClass;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlTypes;
using System.Globalization;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.Remoting.Contexts;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using WATA.LIS.WPS.UHF_RF_Receiver;
using ZeroMQ;

namespace WATA.LIS.WPS
{
    /// <summary>
    /// MainWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MainWindow : Window
    {
        
        public MainWindow()
        {
            InitializeComponent();//0C:DC:7E:1D:C5:B6
            FH920 TableRFID = new FH920("TABLE","34:86:5D:76:CC:1A", "8051", dgPairListTable, dgSendListTable, this.Dispatcher);
            //FH920 TableRFID = new FH920("TABLE", "0C:DC:7E:1D:C5:B6", "8051", dgPairListTable, dgSendListTable, this.Dispatcher);

            TableRFID.Init();


            //FH920 LocationRFID = new FH920("LOCATION","0C:DC:7E:1D:C5:B6", "8052", dgPairListLocation, dgSendListLocation, this.Dispatcher);
            //LocationRFID.Init();
        }
       
    }
}
