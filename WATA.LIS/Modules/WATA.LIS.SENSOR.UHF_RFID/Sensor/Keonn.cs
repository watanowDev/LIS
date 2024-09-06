using Apulsetech.Remote.Type;
using Apulsetech.Remote;
using Apulsetech.Type;
using Prism.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;
using WATA.LIS.Core.Common;
using WATA.LIS.Core.Interfaces;
using WATA.LIS.Core.Model.SystemConfig;
using Apulsetech.Remote.Thread;
using System.Threading;
using Apulsetech.Rfid.Type;
using System.Net;
using System.IO;
using sun.misc;
using System.Net.NetworkInformation;
using System.Xml;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Net.Http;
using Newtonsoft.Json;
using WATA.LIS.Core.Model.BackEnd;

namespace WATA.LIS.SENSOR.UHF_RFID.Sensor
{
    public class Keonn
    {
        private readonly IEventAggregator _eventAggregator;
        private RemoteDeviceScanner mRemoteDeviceScanner;
        private readonly MsgEvent mMsgEvent = new MsgEvent();
        private DispatcherTimer mScanTimeoutTimer;
        private DispatcherTimer mConnectionTimer;
        private DispatcherTimer mStatusCheckTimer;

        private int mTimeout = 30000;
        private RemoteDevice mRemoteDevice;
        private bool mConnected = false;
        private bool mRfidInventoryStarted = false;
        private readonly IRFIDModel _rfidmodel;
        private string mDeviceID;

        RFIDConfigModel rfidConfig;

        private String rfidUri;


        public Keonn(IEventAggregator eventAggregator, IRFIDModel rfidmodel, IMainModel main)
        {
            _eventAggregator = eventAggregator;
            _rfidmodel = rfidmodel;
            rfidConfig = (RFIDConfigModel)_rfidmodel;

            MainConfigModel main_config = (MainConfigModel)main;
        }

        public void Init()
        {
            if (rfidConfig.rfid_enable == 0)
            {
                Tools.Log("rftag disable", Tools.ELogType.RFIDLog);
                return;
            }
            TryConnect();
        }

        private void TryConnect()
        {
            string response;
            response = GetResponseFromURI().Result;

            if (response == string.Empty)
            {
                mConnected = false;
                Tools.Log("Empty String", Tools.ELogType.RFIDLog);
            }
            else
            {
                mConnected = true;
                Tools.Log("Reader Connected", Tools.ELogType.RFIDLog);
            }
        }

        private async Task<string> GetResponseFromURI()
        {
            string response = string.Empty;
            rfidUri = $"http://{rfidConfig.ip}/devices/";

            try
            {
                HttpWebRequest request = WebRequest.CreateHttp(rfidUri);
                request.Method = "GET";
                request.Timeout = 30 * 1000; // 30초
                using (HttpWebResponse resp = (HttpWebResponse)request.GetResponse())
                {
                    if (resp.StatusCode == HttpStatusCode.OK)
                    {
                        using (StreamReader sr = new StreamReader(resp.GetResponseStream()))
                        {
                            await GetDeviceID(sr);
                            await ReaderStart();
                        }
                    }
                }
            }
            catch
            {
                Tools.Log($"Exception Connect!!!", Tools.ELogType.RFIDLog);
                SysError.AddErrorCode(SysError.RFIDConnError);
            }

            return response;
        }

        private Task GetDeviceID(StreamReader sr)
        {
            XmlDocument xmlDocument = new XmlDocument();
            xmlDocument.LoadXml(sr.ReadToEnd());
            XmlNodeList nodeList = xmlDocument.GetElementsByTagName("id");
            if (nodeList.Count > 0)
            {
                mDeviceID = nodeList[0].InnerText;
                Tools.Log($"Success Connection RFID", Tools.ELogType.RFIDLog);
                SysError.RemoveErrorCode(SysError.RFIDConnError);
            }

            return Task.CompletedTask;
        }

        private Task ReaderStart()
        {
            rfidUri = $"http://{rfidConfig.ip}/devices/{mDeviceID}/start";

            try
            {
                HttpWebRequest request = WebRequest.CreateHttp(rfidUri);
                request.Method = "GET";
                request.Timeout = 30 * 1000; // 30초
                using (HttpWebResponse resp = (HttpWebResponse)request.GetResponse())
                {
                    if (resp.StatusCode == HttpStatusCode.OK)
                    {
                        Tools.Log("RF is On", Tools.ELogType.RFIDLog);
                    }
                }
            }
            catch
            {
                Tools.Log($"Failed starting RF", Tools.ELogType.RFIDLog);
            }

            return Task.CompletedTask;
        }

        private async Task ReadEPC()
        {

        }
    }
}
