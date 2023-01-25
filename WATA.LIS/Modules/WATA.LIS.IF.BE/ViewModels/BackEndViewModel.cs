using log4net.Core;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using WATA.LIS.Core.Common;
using WATA.LIS.Core.Events.BackEnd;
using WATA.LIS.Core.Model.BackEnd;
using static System.Net.WebRequestMethods;

namespace WATA.LIS.IF.BE.ViewModels
{
    public class BackEndViewModel : BindableBase
    {
        public ObservableCollection<Log> ListBackEndLog { get; set; }
        public ObservableCollection<Log> ListBackEndCurrentLog { get; set; }
        public DelegateCommand<string> ButtonFunc { get; set; }

        private readonly IEventAggregator _eventAggregator;

        private string m_location = "INCHEON_CALT_001";
        private string m_vihicle = "fork_lift001";
        private string m_errorcode = "0000";

        
        private string _TagInfo;
        public string TagInfo { get { return _TagInfo; } set { SetProperty(ref _TagInfo, value); } }


        private string _DistanceInfo;
        public string DistanceInfo { get { return _DistanceInfo; } set { SetProperty(ref _DistanceInfo, value); } }


        public BackEndViewModel(IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;
            ListBackEndLog = Tools.logInfo.ListBackEndLog;
            ListBackEndCurrentLog = Tools.logInfo.ListBackEndCurrentLog;

            ButtonFunc = new DelegateCommand<string>(ButtonFuncClick);
            Tools.Log($"##########################Init", Tools.ELogType.BackEndLog);
            Tools.Log($"##########################Init", Tools.ELogType.BackEndCurrentLog);
        }

        public void SendAliveEvent()
        {
            AliveModel alive_obj = new AliveModel();
            alive_obj.alive.workLocationId = m_location;
            alive_obj.alive.vehicleId = m_vihicle;
            alive_obj.alive.errorCode = m_errorcode;
            string json_body = Util.ObjectToJson(alive_obj);
            RestClientPostModel post_obj = new RestClientPostModel();
            post_obj.url = "https://dev-lms-api.watalbs.com/monitoring/geofence/addition-info/logistics/heavy-equipment/alive";
            post_obj.body = json_body;
            post_obj.type = eMessageType.BackEndCurrent;
            _eventAggregator.GetEvent<RestClientPostEvent>().Publish(post_obj);
        }

        private void SendAction_IN_InfoEvent(string Tag, string Distance)
        {
            ActionInfoModel action_obj = new ActionInfoModel();
            action_obj.actionInfo.workLocationId = m_location;
            action_obj.actionInfo.vehicleId = m_vihicle;
            action_obj.actionInfo.loadId = "";
            action_obj.actionInfo.containerId = "";
            action_obj.actionInfo.action = "IN";
            action_obj.actionInfo.epc = Tag;
            action_obj.actionInfo.height = Distance;
            action_obj.actionInfo.loadRate = "0";
            action_obj.actionInfo.loadMatrixColumn = "10";
            action_obj.actionInfo.loadMatrixRaw = "10";

            action_obj.actionInfo.loadMatrix.Add(0);
            action_obj.actionInfo.loadMatrix.Add(0);
            action_obj.actionInfo.loadMatrix.Add(0);
            action_obj.actionInfo.loadMatrix.Add(0);
            action_obj.actionInfo.loadMatrix.Add(0);
            action_obj.actionInfo.loadMatrix.Add(0);
            action_obj.actionInfo.loadMatrix.Add(0);
            action_obj.actionInfo.loadMatrix.Add(0);
            action_obj.actionInfo.loadMatrix.Add(0);
            action_obj.actionInfo.loadMatrix.Add(0);


            string json_body = Util.ObjectToJson(action_obj);
            RestClientPostModel post_obj = new RestClientPostModel();
            post_obj.url = "https://dev-lms-api.watalbs.com/monitoring/geofence/addition-info/logistics/heavy-equipment/action";
            post_obj.body = json_body;
            post_obj.type = eMessageType.BackEndAction;
            _eventAggregator.GetEvent<RestClientPostEvent>().Publish(post_obj);


        }

        private void SendAction_OUT_InfoEvent(string Tag, string Distance)
        {


            ActionInfoModel action_obj = new ActionInfoModel();
            action_obj.actionInfo.workLocationId = m_location;
            action_obj.actionInfo.vehicleId = m_vihicle;

            action_obj.actionInfo.loadId = "";

            // action_obj.actionInfo.containerId = "{750.0;Activize;323525.0}";
            action_obj.actionInfo.containerId = "";

            action_obj.actionInfo.action = "OUT";
            action_obj.actionInfo.epc = Tag;
            action_obj.actionInfo.height = Distance;
            action_obj.actionInfo.loadRate = "";
            action_obj.actionInfo.loadRate = "90";
            action_obj.actionInfo.loadMatrixColumn = "10";
            action_obj.actionInfo.loadMatrixRaw = "10";



            action_obj.actionInfo.loadMatrix.Add(0);
            action_obj.actionInfo.loadMatrix.Add(10);
            action_obj.actionInfo.loadMatrix.Add(10);
            action_obj.actionInfo.loadMatrix.Add(10);
            action_obj.actionInfo.loadMatrix.Add(10);
            action_obj.actionInfo.loadMatrix.Add(10);
            action_obj.actionInfo.loadMatrix.Add(10);
            action_obj.actionInfo.loadMatrix.Add(10);
            action_obj.actionInfo.loadMatrix.Add(10);
            action_obj.actionInfo.loadMatrix.Add(0);



            string json_body = Util.ObjectToJson(action_obj);
            RestClientPostModel post_obj = new RestClientPostModel();
            post_obj.url = "https://dev-lms-api.watalbs.com/monitoring/geofence/addition-info/logistics/heavy-equipment/action";
            post_obj.body = json_body;
            post_obj.type = eMessageType.BackEndAction;
            _eventAggregator.GetEvent<RestClientPostEvent>().Publish(post_obj);
        }

        private void SendLocationInfoEvent(string Tag)
        {
            LocationInfoModel location_obj = new LocationInfoModel();

            location_obj.locationInfo.vehicleId = m_vihicle;
            location_obj.locationInfo.workLocationId = m_location;
            location_obj.locationInfo.epc = Tag;

            string json_body = Util.ObjectToJson(location_obj);
            RestClientPostModel post_obj = new RestClientPostModel();
            post_obj.url = "https://dev-lms-api.watalbs.com/monitoring/geofence/addition-info/logistics/heavy-equipment/location";
            post_obj.body = json_body;
            post_obj.type = eMessageType.BackEndAction;
            _eventAggregator.GetEvent<RestClientPostEvent>().Publish(post_obj);
        }

        private void ButtonFuncClick(string command)
        {
            try
            {
                if (command == null) return;
                switch (command)
                {
                    case "Get":

                        SendAliveEvent();

                        break;

                    case "ActionIN":

                        SendAction_IN_InfoEvent(TagInfo, DistanceInfo);
                        break;

                    case "ActionOUT":
                        SendAction_OUT_InfoEvent(TagInfo, DistanceInfo);
                        break;

                    case "Location":

                        SendLocationInfoEvent(TagInfo);

                        break;

                    default:
                        break;
                }
            }
            catch (Exception ex)
            {

            }
        }
    }
}
