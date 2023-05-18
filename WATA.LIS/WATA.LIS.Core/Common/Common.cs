using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WATA.LIS.Core.Common
{
    public enum eSample : Int32
    {
        sample_1 = 1,
        sample_2
    }
    public enum eMessageType : Int32
    {
        BackEndAction,
        BackEndCurrent
    }

    public enum eDeviceType : Int32
    {
        ForkLift_V1,
        ForkLift_V2,
        GateChecker
    }

    public enum eGateActionType : Int32
    {
        IN,
        OUT,
        UnKnown
    }

    public enum eLEDStatus : Int32
    {
        LED_OFF,
        RED,
        GREEN,
        BLUE,
    }
}