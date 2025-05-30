using Newtonsoft.Json.Linq;
using Prism.Events;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
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
            _eventAggregator.GetEvent<RestClientPostEvent_dev>().Subscribe(OnClientPost_dev, ThreadOption.BackgroundThread, true);
            _eventAggregator.GetEvent<RestClientGetEvent>().Subscribe(OnClientGetInt, ThreadOption.BackgroundThread, true);
            _eventAggregator.GetEvent<RestClientGetEvent>().Subscribe(OnClientGetString, ThreadOption.BackgroundThread, true);

        }

        public async void OnClientPost_dev(RestClientPostModel Model)
        {
            Tools.ELogType logtype;
            if (Model.type == eMessageType.BackEndAction)
            {
                logtype = Tools.ELogType.BackEndLog;
            }
            else if (Model.type == eMessageType.BackEndCurrent)
            {
                logtype = Tools.ELogType.BackEndCurrentLog;
            }
            else
            {
                logtype = Tools.ELogType.BackEndLog;
            }
            Tools.Log($"Request Post URI : {Model.url} ", logtype);
            Tools.Log(Model.body, logtype);

            try
            {
                HttpClientHandler handler = new HttpClientHandler();
                handler.ServerCertificateCustomValidationCallback = (message, cert, chain, sslPolicyErrors) => true;

                using (HttpClient client = new HttpClient(handler))
                {
                    client.Timeout = TimeSpan.FromSeconds(30);
                    var content = new StringContent(Model.body, Encoding.ASCII, "application/json");
                    HttpResponseMessage response = await client.PostAsync(Model.url, content);
                    response.EnsureSuccessStatusCode();
                    string responseText = await response.Content.ReadAsStringAsync();
                    _eventAggregator.GetEvent<BackEndStatusEvent>().Publish(1);
                    Tools.Log($"REST Post Client Response Data : {responseText} ", logtype);

                    if (Model.type == eMessageType.BackEndContainer)
                    {
                        ParseContainterJson(responseText);
                    }
                }
            }
            catch (HttpRequestException exception)
            {
                _eventAggregator.GetEvent<BackEndStatusEvent>().Publish(-1);
                Tools.Log($"BE Conn Error!!! REST Post Client Response Error dev", logtype);
                Tools.Log($"Exception: {exception.Message}", logtype);
            }
        }

        public void OnClientPost(RestClientPostModel Model)
        {
            Tools.ELogType logtype;
            if (Model.type == eMessageType.BackEndAction)
            {

                logtype = Tools.ELogType.BackEndLog;
            }
            else
            {
                logtype = Tools.ELogType.BackEndCurrentLog;
            }
            Tools.Log($"Request Post URI : {Model.url} ", logtype);
            Tools.Log(Model.body, logtype);

            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(Model.url);
                request.Method = "POST";
                request.ContentType = "application/json";
                request.Timeout = 3 * 1000;
                byte[] bytes = Encoding.ASCII.GetBytes(Model.body);
                request.ContentLength = bytes.Length; // 바이트수 지정
                using (Stream reqStream = request.GetRequestStream())
                {
                    reqStream.Write(bytes, 0, bytes.Length);
                }
                using (WebResponse resp = request.GetResponse())
                {
                    //var httpWebResponse = (HttpWebResponse)resp;
                    // string characterSet = httpWebResponse.CharacterSet;
                    Stream respStream = resp.GetResponseStream();
                    using (StreamReader sr = new StreamReader(respStream))
                    {
                        string responseText = sr.ReadToEnd();
                        _eventAggregator.GetEvent<BackEndStatusEvent>().Publish(1);
                        Tools.Log($"REST Post Client Response Data : {responseText} ", logtype);
                        if (Model.type == eMessageType.BackEndContainer)
                        {
                            ParseContainterJson(responseText);
                        }
                    }

                }
            }
            catch (WebException exception)
            {/*
                try
                {



                    _eventAggregator.GetEvent<BackEndStatusEvent>().Publish(-1);
                    Tools.Log($"REST Post Client Response Error smp", logtype);
                    using (WebResponse resp = exception.Response)
                    {
                        Stream respStream = resp.GetResponseStream();
                        using (StreamReader sr = new StreamReader(respStream))
                        {
                            string responseText = sr.ReadToEnd();
                            _eventAggregator.GetEvent<BackEndStatusEvent>().Publish(1);
                            Tools.Log($"REST Post Client Response Data : {responseText} ", logtype);

                            if (Model.type == eMessageType.BackEndContainer)
                            {
                                ParseContainterJson(responseText);
                            }
                        }
                    }
                }
                catch { }
                */
            }
        }


        private void ParseContainterJson(string str)
        {
            JObject json = (JObject)JToken.Parse(str);
            int code = (int)json["status"];
            _eventAggregator.GetEvent<BackEndReturnCodeEvent>().Publish(code);
        }


        public void OnClientGetInt(string URL)
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
                        _eventAggregator.GetEvent<BackEndStatusEvent>().Publish(1);
                        Tools.Log($"REST Get Client Response Data : {responseText} ", Tools.ELogType.BackEndLog);
                    }
                }
            }
            catch
            {
                _eventAggregator.GetEvent<BackEndStatusEvent>().Publish(-1);
                Tools.Log($"REST Get Client Response Error", Tools.ELogType.BackEndLog);
            }
        }

        public void OnClientGetString(string url)
        {
            Tools.Log($"Request Get URI : {url}", Tools.ELogType.BackEndLog);
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = "GET";
                request.Timeout = 3 * 1000; // 3초
                using (HttpWebResponse resp = (HttpWebResponse)request.GetResponse())
                {
                    HttpStatusCode status = resp.StatusCode;
                    Stream respStream = resp.GetResponseStream();
                    using (StreamReader sr = new StreamReader(respStream))
                    {
                        string responseText = sr.ReadToEnd();
                        responseText = sr.ReadToEnd();
                        _eventAggregator.GetEvent<RestClientGetEvent>().Publish(responseText);
                        Tools.Log($"REST Response Data : {responseText} ", Tools.ELogType.ActionLog);
                    }
                }
            }
            catch
            {
                _eventAggregator.GetEvent<RestClientGetEvent>().Publish("GetRespFail");
                Tools.Log($"REST Response Error", Tools.ELogType.ActionLog);
            }
        }
    }
}
