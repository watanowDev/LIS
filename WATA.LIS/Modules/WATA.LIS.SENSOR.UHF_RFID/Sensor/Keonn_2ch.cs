using Prism.Events;
using System;
using System.Windows.Threading;
using WATA.LIS.Core.Common;
using WATA.LIS.Core.Interfaces;
using WATA.LIS.Core.Model.SystemConfig;
using WATA.LIS.Core.Model.RFID;
using WATA.LIS.Core.Events.RFID;
using System.IO.Ports;
using ThingMagic;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

namespace WATA.LIS.SENSOR.UHF_RFID.Sensor
{
    public class Keonn_2ch
    {
        private readonly IEventAggregator _eventAggregator;
        private readonly IRFIDModel _rfidmodel;
        private static Reader reader;

        SerialPort serial = new SerialPort();
        SerialPort _port = new SerialPort();

        RFIDConfigModel rfidConfig;

        private DispatcherTimer mCheckConnTimer;
        private DispatcherTimer mGetInventoryTimer;
        private bool mConnected = false;


        public Keonn_2ch(IEventAggregator eventAggregator, IRFIDModel rfidmodel)
        {
            _eventAggregator = eventAggregator;
            _rfidmodel = rfidmodel;
            rfidConfig = (RFIDConfigModel)_rfidmodel;
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
            mCheckConnTimer.Start();

            mGetInventoryTimer = new DispatcherTimer();
            mGetInventoryTimer.Interval = new TimeSpan(0, 0, 0, 0, 1000);
            mGetInventoryTimer.Tick += new EventHandler(TagReadTimer);
            mGetInventoryTimer.Start();

            RfidReaderInit();
        }

        private void RfidReaderInit()
        {
            try
            {
                reader = Reader.Create($"tmr:///{rfidConfig.comport}");
                reader.Connect();

                //The region of operation should be set
                Reader.Region[] readerSupportedRegions = (Reader.Region[])reader.ParamGet("/reader/region/supportedRegions");
                if (readerSupportedRegions.Length < 1)
                    throw new FAULT_INVALID_REGION_Exception();

                //Set region to KR2
                reader.ParamSet("/reader/region/id", readerSupportedRegions[5]);

                //Set antennas 1 or 2
                int[] antennaList = { 1 };
                //int[] antennaList = { 1, 2 };

                //Set use the antennas, use GEN2 protocol, don't use any filter
                SimpleReadPlan plan = new SimpleReadPlan(antennaList, TagProtocol.GEN2, null, null, 1000);
                reader.ParamSet("/reader/read/plan", plan);

                //Get the model of the reader for checking the connection
                string model = (string)reader.ParamGet("/reader/version/model");

                mConnected = true;
                SysAlarm.RemoveErrorCodes(SysAlarm.RFIDStartErr);

                //reader.Destroy();
            }
            catch (Exception ex)
            {
                mConnected = false;
                SysAlarm.AddErrorCodes(SysAlarm.RFIDStartErr);
                Tools.Log($"[RfidReaderInit] Exception !!! : {ex.Message}", Tools.ELogType.RFIDLog);
            }
        }

        private void CheckConnTimer(object sender, EventArgs e)
        {
            try
            {
                //Get the model of the reader for checking the connection
                string model = (string)reader.ParamGet("/reader/version/model");
                mConnected = true;
                SysAlarm.RemoveErrorCodes(SysAlarm.RFIDConnErr);
                Tools.Log($"[RfidReaderConnCheck] Connected !!! : {model}", Tools.ELogType.RFIDLog);
            }
            catch (Exception ex)
            {
                mConnected = false;
                SysAlarm.AddErrorCodes(SysAlarm.RFIDConnErr);
                Tools.Log($"[RfidReaderConnCheck] Exception !!! : {ex.Message}", Tools.ELogType.RFIDLog);
            }
        }

        private async void TagReadTimer(object sender, EventArgs e)
        {
            if (!mConnected)
            {
                return;
            }

            try
            {
                //Start reading
                TagReadData[] tagsRead = await Task.Run(() => reader.Read(800));

                List<TagReadData> filteredTags = new List<TagReadData>();

                foreach (TagReadData tag in tagsRead)
                {
                    if (tag.EpcString.Contains("CB") || tag.EpcString.Contains("DC") || tag.EpcString.Contains("DA")) //CB:컨테이너, DC:도크, DA:랙
                    {
                        filteredTags.Add(tag);
                    }
                }

                // Sort filteredTags by ReadCount in descending order, then by RSSI in descending order
                filteredTags = filteredTags.OrderByDescending(tag => tag.ReadCount)
                                           .ThenByDescending(tag => tag.Rssi)
                                           .ToList();

                PubTagData(filteredTags);
                SysAlarm.RemoveErrorCodes(SysAlarm.RFIDRcvErr);
            }
            catch
            {
                Tools.Log($"[DataRecive] Exception !!!", Tools.ELogType.RFIDLog);
                SysAlarm.AddErrorCodes(SysAlarm.RFIDRcvErr);
            }
        }

        private void PubTagData(List<TagReadData> tagsRead)
        {
            List<Keonn2ch_Model> eventModels = new List<Keonn2ch_Model>();

            foreach (TagReadData tag in tagsRead)
            {
                Keonn2ch_Model keonn2chEventModel = new Keonn2ch_Model();
                keonn2chEventModel.EPC = tag.EpcString;
                keonn2chEventModel.TS = tag.Time;
                keonn2chEventModel.RSSI = tag.Rssi;
                keonn2chEventModel.COUNT = tag.ReadCount;

                eventModels.Add(keonn2chEventModel);
            }

            _eventAggregator.GetEvent<Keonn2chEvent>().Publish(eventModels);
        }
    }
}
