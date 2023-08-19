using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WATA.LIS.Core.Common;

namespace WATA.LIS.SENSOR.WEIGHT.ViewModels
{
    public class WeightViewModel : BindableBase
    {

        public ObservableCollection<Log> ListWeightLog { get; set; }

        public WeightViewModel()
        {
           ListWeightLog = Tools.logInfo.ListWeightLog;
           Tools.Log($"Init Weight View  Model", Tools.ELogType.WeightLog);

        }


    }
}
