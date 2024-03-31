using Newtonsoft.Json;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Documents;
using WATA.LIS.Core.Common;
using WATA.LIS.Core.Events.DPS;
using WATA.LIS.Core.Events.Indicator;
using WATA.LIS.Core.Interfaces;
using WATA.LIS.Core.Model.DPS;
using WATA.LIS.Core.Model.SystemConfig;

namespace WATA.LIS.IF.DPS.ViewModels
{
    public class DPSViewModel : BindableBase
    {
        public ObservableCollection<Log> ListDPSLog { get; set; }

        public DelegateCommand<string> ButtonFunc { get; set; }

        private readonly IEventAggregator _eventAggregator;


        private string _SetClear_L_ID;
        public string SetClear_L_ID
        {
            get { return _SetClear_L_ID; }
            set { SetProperty(ref _SetClear_L_ID, value); }
        }

        private string _SetClear_C_ID;
        public string SetClear_C_ID
        {
            get { return _SetClear_C_ID; }
            set { SetProperty(ref _SetClear_C_ID, value); }
        }

        private string _SetDisplay_L_ID;
        public string SetDisplay_L_ID
        {
            get { return _SetDisplay_L_ID; }
            set { SetProperty(ref _SetDisplay_L_ID, value); }
        }

        private string _SetDisplay_C_ID;
        public string SetDisplay_C_ID
        {
            get { return _SetDisplay_C_ID; }
            set { SetProperty(ref _SetDisplay_C_ID, value); }
        }

        private string _SetDisplay_ADDR1;
        public string SetDisplay_ADDR1
        {
            get { return _SetDisplay_ADDR1; }
            set { SetProperty(ref _SetDisplay_ADDR1, value); }
        }

        private string _SetDisplay_ADDR2;
        public string SetDisplay_ADDR2
        {
            get { return _SetDisplay_ADDR2; }
            set { SetProperty(ref _SetDisplay_ADDR2, value); }
        }

        private string _SetDisplay_Seq;
        public string SetDisplay_Seq
        {
            get { return _SetDisplay_Seq; }
            set { SetProperty(ref _SetDisplay_Seq, value); }
        }

        private string _SetDisplay_Color1;
        public string SetDisplay_Color1
        {
            get { return _SetDisplay_Color1; }
            set { SetProperty(ref _SetDisplay_Color1, value); }
        }

        private string _SetDisplay_Color2;
        public string SetDisplay_Color2
        {
            get { return _SetDisplay_Color2; }
            set { SetProperty(ref _SetDisplay_Color2, value); }
        }

        private string _SetDisplay_Color3;
        public string SetDisplay_Color3
        {
            get { return _SetDisplay_Color3; }
            set { SetProperty(ref _SetDisplay_Color3, value); }
        }

        private string _SetDisplay_Color4;
        public string SetDisplay_Color4
        {
            get { return _SetDisplay_Color4; }
            set { SetProperty(ref _SetDisplay_Color4, value); }
        }

        private string _SetDisplay_Color5;
        public string SetDisplay_Color5
        {
            get { return _SetDisplay_Color5; }
            set { SetProperty(ref _SetDisplay_Color5, value); }
        }

        private string _SetDisplay_Color6;
        public string SetDisplay_Color6
        {
            get { return _SetDisplay_Color6; }
            set { SetProperty(ref _SetDisplay_Color6, value); }
        }

        private string _SetDisplay_Color7;
        public string SetDisplay_Color7
        {
            get { return _SetDisplay_Color7; }
            set { SetProperty(ref _SetDisplay_Color7, value); }
        }

        private string _SetDisplay_Color8;
        public string SetDisplay_Color8
        {
            get { return _SetDisplay_Color8; }
            set { SetProperty(ref _SetDisplay_Color8, value); }
        }

        private string _SetDisplay_Color9;
        public string SetDisplay_Color9
        {
            get { return _SetDisplay_Color9; }
            set { SetProperty(ref _SetDisplay_Color9, value); }
        }

        private string _SetDisplay_Color10;
        public string SetDisplay_Color10
        {
            get { return _SetDisplay_Color10; }
            set { SetProperty(ref _SetDisplay_Color10, value); }
        }

        private string _SetDisplay_Color11;
        public string SetDisplay_Color11
        {
            get { return _SetDisplay_Color11; }
            set { SetProperty(ref _SetDisplay_Color11, value); }
        }

        private string _SetDisplay_Color12;
        public string SetDisplay_Color12
        {
            get { return _SetDisplay_Color12; }
            set { SetProperty(ref _SetDisplay_Color12, value); }
        }

        private string _SetDisplay_Color13;
        public string SetDisplay_Color13
        {
            get { return _SetDisplay_Color13; }
            set { SetProperty(ref _SetDisplay_Color13, value); }
        }

        private string _SetDisplay_Color14;
        public string SetDisplay_Color14
        {
            get { return _SetDisplay_Color14; }
            set { SetProperty(ref _SetDisplay_Color14, value); }
        }

        private string _SetDisplay_Color15;
        public string SetDisplay_Color15
        {
            get { return _SetDisplay_Color15; }
            set { SetProperty(ref _SetDisplay_Color15, value); }
        }

        private string _SetDisplay_Color16;
        public string SetDisplay_Color16
        {
            get { return _SetDisplay_Color16; }
            set { SetProperty(ref _SetDisplay_Color16, value); }
        }

        private string _SetDisplay_UTF1;
        public string SetDisplay_UTF1
        {
            get { return _SetDisplay_UTF1; }
            set { SetProperty(ref _SetDisplay_UTF1, value); }
        }

        private string _SetDisplay_UTF2;
        public string SetDisplay_UTF2
        {
            get { return _SetDisplay_UTF2; }
            set { SetProperty(ref _SetDisplay_UTF2, value); }
        }

        private string _SetDisplay_UTF3;
        public string SetDisplay_UTF3
        {
            get { return _SetDisplay_UTF3; }
            set { SetProperty(ref _SetDisplay_UTF3, value); }
        }

        private string _SetDisplay_UTF4;
        public string SetDisplay_UTF4
        {
            get { return _SetDisplay_UTF4; }
            set { SetProperty(ref _SetDisplay_UTF4, value); }
        }

        private string _SetDisplay_UTF5;
        public string SetDisplay_UTF5
        {
            get { return _SetDisplay_UTF5; }
            set { SetProperty(ref _SetDisplay_UTF5, value); }
        }

        private string _SetDisplay_UTF6;
        public string SetDisplay_UTF6
        {
            get { return _SetDisplay_UTF6; }
            set { SetProperty(ref _SetDisplay_UTF6, value); }
        }

        private string _SetDisplay_UTF7;
        public string SetDisplay_UTF7
        {
            get { return _SetDisplay_UTF7; }
            set { SetProperty(ref _SetDisplay_UTF7, value); }
        }

        private string _SetDisplay_UTF8;
        public string SetDisplay_UTF8
        {
            get { return _SetDisplay_UTF8; }
            set { SetProperty(ref _SetDisplay_UTF8, value); }
        }

        private string _SetDisplay_UTF9;
        public string SetDisplay_UTF9
        {
            get { return _SetDisplay_UTF9; }
            set { SetProperty(ref _SetDisplay_UTF9, value); }
        }

        private string _SetDisplay_UTF10;
        public string SetDisplay_UTF10
        {
            get { return _SetDisplay_UTF10; }
            set { SetProperty(ref _SetDisplay_UTF10, value); }
        }

        private string _SetDisplay_UTF11;
        public string SetDisplay_UTF11
        {
            get
            { return _SetDisplay_UTF11;}
            set { SetProperty(ref _SetDisplay_UTF11, value); }
        }

        private string _SetDisplay_UTF12;
        public string SetDisplay_UTF12
        {
            get { return _SetDisplay_UTF12; }
            set { SetProperty(ref _SetDisplay_UTF12, value); }
        }

        private string _SetDisplay_UTF13;
        public string SetDisplay_UTF13
        {
            get { return _SetDisplay_UTF13; }
            set { SetProperty(ref _SetDisplay_UTF13, value); }
        }

        private string _SetDisplay_UTF14;
        public string SetDisplay_UTF14
        {
            get { return _SetDisplay_UTF14; }
            set { SetProperty(ref _SetDisplay_UTF14, value); }
        }

        private string _SetDisplay_UTF15;
        public string SetDisplay_UTF15
        {
            get { return _SetDisplay_UTF15; }
            set { SetProperty(ref _SetDisplay_UTF15, value); }
        }

        private string _SetDisplay_UTF16;
        public string SetDisplay_UTF16
        {
            get { return _SetDisplay_UTF16; }
            set { SetProperty(ref _SetDisplay_UTF16, value); }
        }

        private void str_init()
        {
            SetClear_L_ID = "30";
            SetClear_C_ID = "33";
            SetDisplay_L_ID = "30";
            SetDisplay_C_ID = "33";
            SetDisplay_ADDR1 = "1";
            SetDisplay_ADDR2 = "1";
            SetDisplay_Seq = "0";
            SetDisplay_Color1 = "0";
            SetDisplay_Color2 = "1";
            SetDisplay_Color3 = "2";
            SetDisplay_Color4 = "0";
            SetDisplay_Color5 = "0"; 
            SetDisplay_Color6 = "0";
            SetDisplay_Color7 = "0";
            SetDisplay_Color8 = "0"; 
            SetDisplay_Color9 = "0";
            SetDisplay_Color10 = "0";
            SetDisplay_Color11 = "0";
            SetDisplay_Color12 = "0";
            SetDisplay_Color13 = "0";
            SetDisplay_Color14 = "0";
            SetDisplay_Color15 = "0";
            SetDisplay_Color16 = "0";
            SetDisplay_UTF1 = "입";
            SetDisplay_UTF2 = "고";
            SetDisplay_UTF3 = "1";
            SetDisplay_UTF4 = "입";
            SetDisplay_UTF5 = "고";
            SetDisplay_UTF6 = "1";
            SetDisplay_UTF7 = "입";
            SetDisplay_UTF8 = "고";
            SetDisplay_UTF9 = "1";
            SetDisplay_UTF10 = "입";
            SetDisplay_UTF11 = "고";
            SetDisplay_UTF12 = "1";
            SetDisplay_UTF13 = "출";
            SetDisplay_UTF14 = "고";
            SetDisplay_UTF15 = "2";
            SetDisplay_UTF16 = "입";
        }

        

                


        public DPSViewModel(IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;

            Tools.Log($"Init DPS Model", Tools.ELogType.DPSLog);
            ListDPSLog = Tools.logInfo.ListDPSLog;
            ButtonFunc = new DelegateCommand<string>(ButtonFuncClick);
            str_init();

        }


        private void SetClearTest()
        {
            Tools.Log($"SetClearTest", Tools.ELogType.DPSLog);


            DPSAllClearModel model_obj = new DPSAllClearModel();
            model_obj.payload.AckType = 0; //REQUEST
            model_obj.payload.ControllerID = 30;
            model_obj.payload.LocationID = 233;
            byte[] target = Util.ObjectToByte(model_obj);
            _eventAggregator.GetEvent<DPSSendEvent>().Publish(target);
        }


        private byte[] ConvertASCII(byte[] temp)
        {

            byte[] ret = new byte[3];
 


            if (temp.Length == 3)
            {
                ret[0] = temp[0];
                ret[1] = temp[1];
                ret[2] = temp[2];
            }
            else if (temp.Length == 2)
            {
                ret[0] = 0x00;
                ret[1] = temp[0];
                ret[2] = temp[1];
            }
            else if (temp.Length == 1)
            {
                ret[0] = 0x00;
                ret[1] = 0x00;
                ret[2] = temp[0];
            }

            return ret;
        }

        private void SetDisplaytest()
        {
            Tools.Log($"SetDisplaytest", Tools.ELogType.DPSLog);

            SetDisplayModel model_obj = new SetDisplayModel();

           
            model_obj.payload.AckType = 0;
            model_obj.payload.LocationID = UInt16.Parse(SetDisplay_L_ID);
            model_obj.payload.ControllerID = UInt16.Parse(SetDisplay_C_ID);
            model_obj.payload.ADDR1 = byte.Parse(SetDisplay_ADDR1);
            model_obj.payload.ADDR2 = byte.Parse(SetDisplay_ADDR2);
            model_obj.payload.SEQ = byte.Parse(SetDisplay_Seq);
            model_obj.payload.COLORSET1 = byte.Parse(SetDisplay_Color1);
            model_obj.payload.COLORSET2 = byte.Parse(SetDisplay_Color2);
            model_obj.payload.COLORSET3 = byte.Parse(SetDisplay_Color3);
            model_obj.payload.COLORSET4 = byte.Parse(SetDisplay_Color4);
            model_obj.payload.COLORSET5 = byte.Parse(SetDisplay_Color5);
            model_obj.payload.COLORSET6 = byte.Parse(SetDisplay_Color6);
            model_obj.payload.COLORSET7 = byte.Parse(SetDisplay_Color7);
            model_obj.payload.COLORSET8 = byte.Parse(SetDisplay_Color8);
            model_obj.payload.COLORSET9 = byte.Parse(SetDisplay_Color9);
            model_obj.payload.COLORSET10 = byte.Parse(SetDisplay_Color10);
            model_obj.payload.COLORSET11 = byte.Parse(SetDisplay_Color11);
            model_obj.payload.COLORSET12 = byte.Parse(SetDisplay_Color12);
            model_obj.payload.COLORSET13 = byte.Parse(SetDisplay_Color13);
            model_obj.payload.COLORSET14 = byte.Parse(SetDisplay_Color14);
            model_obj.payload.COLORSET15 = byte.Parse(SetDisplay_Color15);
            model_obj.payload.COLORSET16 = byte.Parse(SetDisplay_Color16);

            model_obj.payload.UTF1 = ConvertASCII(Encoding.UTF8.GetBytes(SetDisplay_UTF1));
            model_obj.payload.UTF2 = ConvertASCII(Encoding.UTF8.GetBytes(SetDisplay_UTF2));
            model_obj.payload.UTF3 = ConvertASCII(Encoding.UTF8.GetBytes(SetDisplay_UTF3));
            model_obj.payload.UTF4 = ConvertASCII(Encoding.UTF8.GetBytes(SetDisplay_UTF4));
            model_obj.payload.UTF5 = ConvertASCII(Encoding.UTF8.GetBytes(SetDisplay_UTF5));
            model_obj.payload.UTF6 = ConvertASCII(Encoding.UTF8.GetBytes(SetDisplay_UTF6));
            model_obj.payload.UTF7 = ConvertASCII(Encoding.UTF8.GetBytes(SetDisplay_UTF7));
            model_obj.payload.UTF8 = ConvertASCII(Encoding.UTF8.GetBytes(SetDisplay_UTF8));
            model_obj.payload.UTF9 = ConvertASCII(Encoding.UTF8.GetBytes(SetDisplay_UTF9));
            model_obj.payload.UTF10 = ConvertASCII(Encoding.UTF8.GetBytes(SetDisplay_UTF10));
            model_obj.payload.UTF11 = ConvertASCII(Encoding.UTF8.GetBytes(SetDisplay_UTF11));
            model_obj.payload.UTF12 = ConvertASCII(Encoding.UTF8.GetBytes(SetDisplay_UTF12));
            model_obj.payload.UTF13 = ConvertASCII(Encoding.UTF8.GetBytes(SetDisplay_UTF13));
            model_obj.payload.UTF14 = ConvertASCII(Encoding.UTF8.GetBytes(SetDisplay_UTF14));
            model_obj.payload.UTF15 = ConvertASCII(Encoding.UTF8.GetBytes(SetDisplay_UTF15));
            model_obj.payload.UTF16 = ConvertASCII(Encoding.UTF8.GetBytes(SetDisplay_UTF16));

            byte[] target = Util.ObjectToByte(model_obj);
            _eventAggregator.GetEvent<DPSSendEvent>().Publish(target);
        }


        private void ButtonFuncClick(string command)
        {
            try
            {
                if (command == null) return;
                switch (command)
                {
                    case "SetClear":
                        SetClearTest();

                        break;

                    case "SetDisplay":
                        SetDisplaytest();


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
