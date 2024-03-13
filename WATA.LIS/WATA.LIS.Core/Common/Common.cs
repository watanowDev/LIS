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
        BackEndCurrent,
        BackEndContainer
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

    public enum eLEDColors
    {
        OFF = 0,
        Red = 1,
        Green = 2,
        Amber = 3,
        Blue = 4,
        Purple = 5,
        Cyan = 6,
        Clear = 7,
        Other = 0xF
    }

    public enum eLEDPatterns
    {
        OFF = 0,
        Continuous = 1,
        Pattern1 = 2,
        Pattern2 = 3,
        Pattern3 = 4,
        Pattern4 = 5,
        Pattern5 = 6,
        Pattern6 = 7,
        Other = 0xF
    }

    public enum eBuzzerPatterns
    {
        OFF = 0,
        Continuous = 1,
        Pattern1 = 2,
        Pattern2 = 3,
        Pattern3 = 4,
        Pattern4 = 5,
        Pattern5 = 6,
        Pattern6 = 7,
        Other = 0xF
    }
    public enum ePlayBuzzerLed
    {
        ACTION_FAIL,
        ACTION_START,
        ACTION_FINISH,
        DROP,
        EMERGENCY,
        EMERGENCY2,
        MEASRUE_OK
    }
}