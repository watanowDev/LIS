using NetMQ.Sockets;
using RFID.Service.IInterface.BLE.IClass;
using RFID.Service.IInterface.BLE;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RFID.Service;
using System.Globalization;
using NetMQ;
using System.Windows.Threading;
using System.Data;
using System.Windows.Controls;

namespace WATA.LIS.WPS.UHF_RF_Receiver
{
    public  class FH920
    {
        private ReaderService _ReaderService;
        private IBLE _IBLE;
        private Thread SearchThread;
        private Thread senderThread;
        private string m_target_uuid;
        private string m_port;
        private string m_topic;
        
        DataTable ParingLogTable = new DataTable();
        DataTable SendLogTable = new DataTable();
        DataGrid dgPairList;
        DataGrid dgSendList;
        Dispatcher dispatcher;
        public FH920(string topic,string uuid, string ZMQPort, DataGrid pair, DataGrid send, Dispatcher dispat)
        {
            m_topic = topic;
            m_target_uuid = uuid;
            dgPairList = pair;
            dgSendList = send;
            dispatcher = dispat;
            m_port = ZMQPort;

            ParingLogTable.Columns.Add("Time", typeof(string));
            ParingLogTable.Columns.Add("Value", typeof(string));
            SendLogTable.Columns.Add("Time", typeof(string));
            SendLogTable.Columns.Add("Value", typeof(string));
            dgPairList.ItemsSource = ParingLogTable.DefaultView;
            dgSendList.ItemsSource = SendLogTable.DefaultView;
        }


        public void Init()
        {
            _ReaderService = new ReaderService();
            SearchConnectStart();
            senderThread = new Thread(new ThreadStart(ZMQInit));
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

                    if (device_uuid == m_target_uuid)
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
                Retry();
            }
        }

        private void Retry()
        {
            if (_IBLE != null)
            {
                _IBLE.EnumerateStop();
                Thread.Sleep(1000);
                _IBLE.Close();
                Thread.Sleep(1000);

                this._IBLE = new IBLE(DeviceSelector.BluetoothLeUnpairedOnly);
                this._IBLE.DeviceAdded += OnBLEDeviceAdded;
                _IBLE.EnumerateStart();
            }
        }

        private async Task RunOnUiThread(Action a)
        {
            await this.dispatcher.InvokeAsync(() =>
            {
                a();
            });
        }




        private void AddParingLog(string value)
        {
            dispatcher.Invoke(DispatcherPriority.Normal, new Action(() =>
            {
                ParingLogTable.Rows.Add(new string[] { DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), value });
                //Scroll Focus
                dgPairList.SelectedIndex = dgPairList.Items.Count - 1;
                dgPairList.ScrollIntoView(dgPairList.Items[dgPairList.Items.Count - 1]);
            }));
        }

        private void AddSendLog(string value)
        {
            dispatcher.Invoke(DispatcherPriority.Normal, new Action(() =>
            {
                SendLogTable.Rows.Add(new string[] { DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), value });
                //Scroll Focus
                dgSendList.SelectedIndex = dgSendList.Items.Count - 1;
                dgSendList.ScrollIntoView(dgSendList.Items[dgSendList.Items.Count - 1]);
            }));
        }





        private byte[] ReqCommandU()
        {
            byte[] RecieveHex = { 0, };


            if (_IBLE == null)
            {
                return RecieveHex;
            }

            if (_IBLE.IsConnected)
            {
                this._IBLE.Send(this._ReaderService.CommandU(), ReaderModule.CommandType.Normal);
                RecieveHex = this._IBLE.Receive();
            }
            else
            {
                Retry();
            }
            return RecieveHex;
        }

        private byte[] ReqCommandQ()
        {
            byte[] RecieveHex = { 0, };

            if (_IBLE == null)
            {

                return RecieveHex;
            }


            if (_IBLE.IsConnected)
            {
                this._IBLE.Send(this._ReaderService.CommandQ(), ReaderModule.CommandType.Normal);
                RecieveHex = this._IBLE.Receive();
            }
            else
            {
                Retry();
            }

            return RecieveHex;
        }




        private void ZMQInit()
        {
            using (var pubSocket = new PublisherSocket())
            {
                Console.WriteLine("Publisher socket binding...");
                pubSocket.Options.SendHighWatermark = 10000;
                //pubSocket.Options.
                pubSocket.Bind("tcp://*:"+ m_port);


                while (true)
                {
                    byte[] frame = ReqCommandQ();

                    if (frame != null)
                    {
                        if (frame.Length > 20)
                        {
                            byte[] RetTemp = new byte[24];
                            System.Buffer.BlockCopy(frame, 6, RetTemp, 0, 24);
                            pubSocket.SendMoreFrame(m_topic).SendFrame(RetTemp);
                            AddSendLog(BytesToString(frame));
                        }
                    }
                    Thread.Sleep(10);
                }
            }
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
