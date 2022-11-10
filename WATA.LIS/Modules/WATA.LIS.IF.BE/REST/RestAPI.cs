using Prism.Events;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;
using WATA.LIS.Core.Common;
using WATA.LIS.Core.Events.BackEnd;
using WATA.LIS.Core.Events.RFID;
using WATA.LIS.Core.Model;
using WATA.LIS.Core.Model.BackEnd;
using WATA.LIS.Core.Model.RFID;

namespace WATA.LIS.IF.BE.REST
{
    public class RestAPI
    {
        private readonly IEventAggregator _eventAggregator;
        public RestAPI(IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;

        }

        public void init()
        {
            _eventAggregator.GetEvent<RestClientPostEvent>().Subscribe(OnClientPost, ThreadOption.BackgroundThread, true);
            _eventAggregator.GetEvent<RestClientGetEvent>().Subscribe(OnClientGet, ThreadOption.BackgroundThread, true);

        }

        public void OnClientPost(RestClientPostModel Model)
        {
            Tools.Log($"Request Post URI : {Model.url}  Body : {Model.body}", Tools.ELogType.BackEndLog);
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(Model.url);
                request.Method = "POST";
                request.ContentType = "application/json";
                request.Timeout = 30 * 1000;
                byte[] bytes = Encoding.ASCII.GetBytes(Model.body);
                request.ContentLength = bytes.Length; // 바이트수 지정
                using (Stream reqStream = request.GetRequestStream())
                {
                    reqStream.Write(bytes, 0, bytes.Length);
                }
                using (WebResponse resp = request.GetResponse())
                {
                    Stream respStream = resp.GetResponseStream();
                    using (StreamReader sr = new StreamReader(respStream))
                    {
                        string responseText = sr.ReadToEnd();
                        Tools.Log($"REST Poist Client Response Data : {responseText} ", Tools.ELogType.BackEndLog);
                    }
                }
            }
            catch
            {

                Tools.Log($"REST Poist Client Response Error", Tools.ELogType.BackEndLog);

            }
            

        }
        public void OnClientGet(string URL)
        {
            Tools.Log($"Request Get URI : {URL}", Tools.ELogType.BackEndLog);
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(URL);
                request.Method = "GET";
                request.Timeout = 30 * 1000; // 30초
                using (HttpWebResponse resp = (HttpWebResponse)request.GetResponse())
                {
                    HttpStatusCode status = resp.StatusCode;
                    Console.WriteLine(status);  // 정상이면 "OK"

                    Stream respStream = resp.GetResponseStream();
                    using (StreamReader sr = new StreamReader(respStream))
                    {
                        string responseText = sr.ReadToEnd();
                        responseText = sr.ReadToEnd();
                        Tools.Log($"REST Get Client Response Data : {responseText} ", Tools.ELogType.BackEndLog);
                    }
                }
            }
            catch
            {
                Tools.Log($"REST Get Client Response Error", Tools.ELogType.BackEndLog);
            }
        }
    }
}
