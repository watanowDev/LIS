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
using Windows.UI.WindowManagement;
using WATA.LIS.Core.Model.RIFID;

namespace WATA.LIS.SENSOR.UHF_RFID.Sensor
{
    public class Keonn_4ch
    {
        private readonly IEventAggregator _eventAggregator;
        private readonly IRFIDModel _rfidmodel;

        RFIDConfigModel rfidConfig;

        private DispatcherTimer mCheckConnTimer;
        private DispatcherTimer mGetInventoryTimer;
        private bool mConnected = false;
        private string mDeviceID;


        public Keonn_4ch(IEventAggregator eventAggregator, IRFIDModel rfidmodel, IMainModel main)
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

            mCheckConnTimer = new DispatcherTimer();
            mCheckConnTimer.Interval = new TimeSpan(0, 0, 0, 0, 30000);
            mCheckConnTimer.Tick += new EventHandler(CheckConnTimer);

            mGetInventoryTimer = new DispatcherTimer();
            mGetInventoryTimer.Interval = new TimeSpan(0, 0, 0, 0, 6000);
            mGetInventoryTimer.Tick += new EventHandler(GetInventoryTimer);

            RfidReaderInit();
        }

        /// <summary>
        /// Initialize RFID Reader
        /// </summary>
        private void RfidReaderInit()
        {
            bool GetDeviceIDResult;
            bool SendStartCommandResult;

            GetDeviceIDResult = GetDeviceID();
            SendStartCommandResult = SendStartCommand();

            if (GetDeviceIDResult && SendStartCommandResult)
            {
                mConnected = true;
                mCheckConnTimer.Start();
                mGetInventoryTimer.Start();
            }
            else
            {
                mConnected = false;
            }
        }

        private bool GetDeviceID()
        {
            bool result = false;
            string rfidUri = $"http://{rfidConfig.ip}/devices/";

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
                            XmlDocument xmlDocument = new XmlDocument();
                            xmlDocument.LoadXml(sr.ReadToEnd());
                            XmlNodeList nodeList = xmlDocument.GetElementsByTagName("id");
                            if (nodeList.Count > 0)
                            {
                                mDeviceID = nodeList[0].InnerText;
                                result = true;
                                Tools.Log($"Device ID : {mDeviceID}", Tools.ELogType.RFIDLog);
                                SysAlarm.RemoveErrorCodes(SysAlarm.RFIDRcvErr);
                            }
                        }
                    }
                }
            }
            catch
            {
                Tools.Log($"Exception Connect!!!", Tools.ELogType.RFIDLog);
                SysAlarm.AddErrorCodes(SysAlarm.RFIDRcvErr);
            }
            return result;
        }

        private bool SendStartCommand()
        {
            bool result = false;
            string rfidUri = $"http://{rfidConfig.ip}/devices/{mDeviceID}/start";

            try
            {
                // Bypassing firewall by disabling the check for certificate validation
                ServicePointManager.ServerCertificateValidationCallback += (sender, certificate, chain, sslPolicyErrors) => true;

                HttpWebRequest request = WebRequest.CreateHttp(rfidUri);
                request.Method = "GET";
                request.Timeout = 30 * 1000; // 30초
                using (HttpWebResponse resp = (HttpWebResponse)request.GetResponse())
                {
                    if (resp.StatusCode == HttpStatusCode.OK)
                    {
                        result = true;
                        Tools.Log("RF is On", Tools.ELogType.RFIDLog);
                        SysAlarm.RemoveErrorCodes(SysAlarm.RFIDStartErr);
                    }
                }
            }
            catch
            {
                Tools.Log($"Failed starting RF", Tools.ELogType.RFIDLog);
                SysAlarm.AddErrorCodes(SysAlarm.RFIDStartErr);
            }
            return result;
        }

        /// <summary>
        /// Check RFID Reader Connection
        /// </summary>
        private void CheckConnTimer(object sender, EventArgs e)
        {
            GetDeviceStatus();
        }

        private string GetDeviceStatus()
        {
            string result = string.Empty;
            string rfidUri = $"http://{rfidConfig.ip}/status/";

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
                            XmlDocument xmlDocument = new XmlDocument();
                            xmlDocument.LoadXml(sr.ReadToEnd());
                            XmlNodeList nodeList = xmlDocument.GetElementsByTagName("status");
                            if (nodeList.Count > 0)
                            {
                                result = nodeList[0].InnerText;
                                SysAlarm.RemoveErrorCodes(SysAlarm.RFIDConnErr);
                            }
                        }
                    }
                }
            }
            catch
            {
                Tools.Log($"Exception Connect!!!", Tools.ELogType.RFIDLog);
                SysAlarm.AddErrorCodes(SysAlarm.RFIDConnErr);
            }
            return result;
        }

        /// <summary>
        /// Get Inventory
        /// </summary>
        private async void GetInventoryTimer(object sender, EventArgs e)
        {
            //await GetInventory();
            await GetRandomInventoryAsync();
        }

        private async Task<List<KeonnSensorModel>> GetInventory()
        {
            List<KeonnSensorModel> inventory = new List<KeonnSensorModel>();



            return inventory;
        }

        /// <summary>
        /// Test Method
        /// </summary>
        private async Task<List<KeonnSensorModel>> GetRandomInventoryAsync()
        {
            // Test Method
            List<KeonnSensorModel> result = new List<KeonnSensorModel>();
            string rfidUri = $"http://{rfidConfig.ip}/devices/{mDeviceID}/jsonMinLocationRandom";

            try
            {
                HttpWebRequest request = WebRequest.CreateHttp(rfidUri);
                request.Method = "GET";
                request.Timeout = 30 * 1000; // 30초
                using (HttpWebResponse resp = (HttpWebResponse)await request.GetResponseAsync())
                {
                    if (resp.StatusCode == HttpStatusCode.OK)
                    {
                        using (StreamReader sr = new StreamReader(resp.GetResponseStream()))
                        {
                            XmlDocument xmlDocument = new XmlDocument();
                            xmlDocument.LoadXml(sr.ReadToEnd());
                            string resultJson = xmlDocument.SelectSingleNode("//result").InnerText;
                            result = JsonConvert.DeserializeObject<List<KeonnSensorModel>>(resultJson);
                        }
                    }
                }
            }
            catch
            {
                Tools.Log($"Failed getting RandomData", Tools.ELogType.RFIDLog);
                SysAlarm.AddErrorCodes(SysAlarm.RFIDRcvErr);
            }
            return result;
        }
    }
}
