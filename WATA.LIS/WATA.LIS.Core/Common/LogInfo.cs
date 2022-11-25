using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WATA.LIS.Core.Common
{
    public class LogInfo : BindableBase
    {
        public ObservableCollection<Log> ListSystemLog;
        public ObservableCollection<Log> ListRFIDLog;
        public ObservableCollection<Log> ListDistanceLog;
        public ObservableCollection<Log> ListVisionLog;
        public ObservableCollection<Log> ListBackEndLog;
        public ObservableCollection<Log> ListBackEndCurrentLog;
        public LogInfo()
        {
            ListSystemLog = new ObservableCollection<Log>();
            ListRFIDLog = new ObservableCollection<Log>();
            ListDistanceLog = new ObservableCollection<Log>();
            ListVisionLog = new ObservableCollection<Log>();
            ListBackEndLog = new ObservableCollection<Log>();
            ListBackEndCurrentLog = new ObservableCollection<Log>();
        }
    }
}

public class Log
{
    public string DateTime { get; set; }

    public string Method { get; set; }

    public string Content { get; set; }

    public Log(string date, string method, string content)
    {
        DateTime = date;
        Method = method;
        Content = content;
    }
}


