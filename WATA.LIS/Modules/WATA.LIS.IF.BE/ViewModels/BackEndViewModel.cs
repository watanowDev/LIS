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

namespace WATA.LIS.IF.BE.ViewModels
{
    public class BackEndViewModel : BindableBase
    {
        public ObservableCollection<Log> ListBackEndLog { get; set; }
        public DelegateCommand<string> ButtonFunc { get; set; }

        private readonly IEventAggregator _eventAggregator;

        private string m_location = "INCHEON_CALT_001";
        private string m_vihicle = "fork_lift001";
        private string m_errorcode = "0000";

        public BackEndViewModel(IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;

            ListBackEndLog = Tools.logInfo.ListBackEndLog;
            Tools.Log($"Init BackEndViewModel", Tools.ELogType.BackEndLog);
            ButtonFunc = new DelegateCommand<string>(ButtonFuncClick);

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
            _eventAggregator.GetEvent<RestClientPostEvent>().Publish(post_obj);
        }

        public void SendActionInfoEvent()
        {
           
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

                    case "Post":

                        SendActionInfoEvent();
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
