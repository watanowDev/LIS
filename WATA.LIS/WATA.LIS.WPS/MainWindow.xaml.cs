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
using ZeroMQ;

namespace WATA.LIS.WPS
{
    /// <summary>
    /// MainWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MainWindow : Window
    {
        private ReaderService _ReaderService;
        private IBLE _IBLE;
        private Thread SearchThread;
        private string target_uuid = "0C:DC:7E:1D:C5:B6";

        DataTable ParingLogTable = new DataTable();
        DataTable SendLogTable = new DataTable();
        DispatcherTimer Send_Timer = new DispatcherTimer();
        Thread senderThread;


        public MainWindow()
        {
            InitializeComponent();

            _ReaderService = new ReaderService();


            ParingLogTable.Columns.Add("Time", typeof(string));
            ParingLogTable.Columns.Add("Value", typeof(string));
            SendLogTable.Columns.Add("Time", typeof(string));
            SendLogTable.Columns.Add("Value", typeof(string));
            dgPairList.ItemsSource = ParingLogTable.DefaultView;
            dgSendList.ItemsSource = SendLogTable.DefaultView;
            SearchConnectStart();
            //Send_Timer.Interval = TimeSpan.FromTicks(500);
            //Send_Timer.Tick += new EventHandler(SendTimerEvent);

            senderThread = new Thread(new ThreadStart(ZMQInit));
          
        }

        private void ZMQInit()
        {
            using (var pubSocket = new PublisherSocket())
            {
                Console.WriteLine("Publisher socket binding...");
                pubSocket.Options.SendHighWatermark = 10000;
                //pubSocket.Options.
                pubSocket.Bind("tcp://*:8051");


                while (true)
                {

                    byte[] frame = ReqCommandU();
                    if (frame.Length > 20)
                    {
                        byte[] RetTemp = new byte[24];
                        System.Buffer.BlockCopy(frame, 6, RetTemp, 0, 24);


                        pubSocket.SendMoreFrame("RFID").SendFrame(RetTemp);
                        AddSendLog(BytesToString(RetTemp));
                    }
                    
                    Thread.Sleep(50);
                }
            }

            /*
            using (var context = ZContext.Create())
            using (var socket = new ZSocket(context, ZSocketType.PUB))
            {
                socket.Bind(string.Format("tcp://{0}", "127.0.0.1:8051"));

                while (true)
                {
                    byte[] frame = ReqCommandU();
                    if (frame.Length > 20)
                    {
                        socket.Send(new ZFrame(frame));
                        AddSendLog(BytesToString(frame));
                    }
                    Thread.Sleep(300);
                }
            }
            */
        }

        private void SendTimerEvent(object sender, EventArgs e)
        {
            ReqCommandU();
        }


        private void AddParingLog(string value)
        {
            Dispatcher.Invoke(DispatcherPriority.Normal, new Action(() =>
            {
                ParingLogTable.Rows.Add(new string[] { DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), value });
                //Scroll Focus
                dgPairList.SelectedIndex = dgPairList.Items.Count - 1;
                dgPairList.ScrollIntoView(dgPairList.Items[dgPairList.Items.Count - 1]);
            }));
        }

        private void AddSendLog(string value)
        {
            Dispatcher.Invoke(DispatcherPriority.Normal, new Action(() =>
            {
                SendLogTable.Rows.Add(new string[] { DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), value });
                //Scroll Focus
                dgSendList.SelectedIndex = dgSendList.Items.Count - 1;
                dgSendList.ScrollIntoView(dgSendList.Items[dgSendList.Items.Count - 1]);
            }));
        }

        private void SearchConnectStart()
        {
            SearchThread = new Thread(DoBLEEnumerateWork)
            {
                IsBackground = true
            };
            SearchThread.Start();
        }

        private void DoBLEEnumerateWork()
        {

            if (this._IBLE != null)
                this._IBLE.EnumerateStop();
            this._IBLE = new IBLE(DeviceSelector.BluetoothLeUnpairedOnly);
            this._IBLE.DeviceAdded += OnBLEDeviceAdded;
            this._IBLE.EnumerateStart();
        }

        private async void OnBLEDeviceAdded(object sender, RFID.Service.IInterface.BLE.Events.DeviceAddedEventArgs e)
        {
            await RunOnUiThread(() =>
            {
                if (!String.IsNullOrEmpty(e.Device.Name))
                {
                    string str = e.Device.UUID.Substring(e.Device.UUID.Length - 17);
                    string name = e.Device.Name;
                    string uuid = e.Device.UUID;
                    string device_uuid = str.ToUpper(CultureInfo.CurrentCulture);

                    AddParingLog("Paring Search : " + uuid);

                    if (device_uuid == target_uuid)
                    {
                        ConnectAsync(uuid);
                    }
                }
            }).ConfigureAwait(false);
        }

        private async Task ConnectAsync(string uuid)
        {
            var result = await _IBLE.ConnectAsync(uuid).ConfigureAwait(false);
            if (result.IsConnected)
            {
                AddParingLog("Connect Success !!!!: " + uuid);
                _IBLE.EnumerateStop();
                senderThread.Start();
            }
            else
            {
                AddParingLog("Connect Failed. : " + uuid);
                _IBLE.EnumerateStop();
               // _IBLE.EnumerateStart();
            }
        }

        private async Task RunOnUiThread(Action a)
        {
            await this.Dispatcher.InvokeAsync(() =>
            {
                a();
            });
        }

        private byte[] ReqCommandU()
        {
            byte[] RecieveHex = { 0, };

            if (_IBLE.IsConnected)
            {
                this._IBLE.Send(this._ReaderService.CommandU(), ReaderModule.CommandType.Normal);
                RecieveHex = this._IBLE.Receive();
                
            }
            
            return RecieveHex;
        }



        private void Button_Click(object sender, RoutedEventArgs e)
        {
            ReqCommandU();
        }
        private string BytesToString(byte[] HexData)
        {
            String strData = "";
            for (int i = 0; i < HexData.Length; i++)
            {
                //strData += String.Format("{0:x2} ", HexData[i]);
                strData += (Convert.ToChar(HexData[i])).ToString();
            }

            return strData;
        }
    }
}
