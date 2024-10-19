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
        Pattern1 = 2,   // -- -- -- --
        Pattern2 = 3,   // -  -  -  -
        Pattern3 = 4,   // .. .. .. ..
        Pattern4 = 5,   // .  .  .  .
        Pattern5 = 6,   // ,  ,  ,  ,
        Pattern6 = 7,   // ,. ,. ,. ,.
        Other = 0xF
    }

    public enum eBuzzerPatterns
    {
        OFF = 0,
        Continuous = 1, //Beep-
        Pattern1 = 2,   //Bee~ ing~
        Pattern2 = 3,   //Be Be Be Be Beep!
        Pattern3 = 4,   //Beep-! Beep-! Beep-! Beep-! Beep-!
        Pattern4 = 5,   //Beep, Beep, Beep, Beep, Beep
        Pattern5 = 6,   //Twinkle Twinkle Little Star Song
        Pattern6 = 7,   //How's the weather Song
        Other = 0xF
    }
    public enum ePlayBuzzerLed
    {
        PIKCUP,
        MEASRUE_OK,
        SET_ITEM,
        NO_QR_PICKUP,
        NO_QR_MEASRUE_OK,
        DROP,
        ACTION_START,
        ACTION_FINISH,
        ACTION_FAIL,
        SEONSOR_ERROR,
        EMERGENCY,
        EMERGENCY2,
        CLEAR_ITEM
    }

    public enum ePlayBuzzerLed_NXDPOC
    {
        SIZE_CHECK_START,
        WEIGHT_CHECK_START,
        SUCCESS,
        FAIL,
        DROP,
        EMERGENCY,
        EMERGENCY2,
        MEASRUE_OK
    }



    public enum ePlayInfoSpeaker
    {
        size_check_complete,
        size_check_start,
        weight_check_complete,
        weight_check_start,
        weight_check_fail,
        qr_check_fail,
        dummy,
    }

    public enum eContainerState
    {
        NONE,
        CONTAINER_IN,
        CONTAINER_OUT
    }

    public enum eDockContainerProcedure
    {
        NONE,
        DOCK_IN,
        CONTAINER_IN,
        CONTAINER_OUT
    }

}