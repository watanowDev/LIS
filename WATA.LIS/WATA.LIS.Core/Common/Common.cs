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
        NORMAL,
        SIZE_CHECK_START,
        SIZE_MEASURE_OK,
        CHECK_COMPLETE,
        QR_PIKCUP,
        QR_MEASURE_OK,
        NO_QR_PICKUP,
        NO_QR_MEASURE_OK,
        NO_QR_CHECK_COMPLETE,
        SET_ITEM,
        SET_ITEM_NORMAL,
        SET_ITEM_PICKUP,
        SET_ITEM_SIZE_CHECK_START,
        SET_ITEM_MEASURE_OK,
        SET_ITEM_CHECK_COMPLETE,
        CLEAR_ITEM,
        DROP,
        ACTION_START,
        ACTION_FINISH,
        ACTION_FAIL,
        EMERGENCY,
        EMERGENCY2,
        DEVICE_ERROR,
        DEVICE_ERROR_CLEAR
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
        stop, measure_complete,
        qr_check_start, qr_check_complete, qr_check_error,
        weight_check_start, weight_check_complete, weight_check_error,
        size_check_start, size_check_start_please_stop, size_check_complete, size_check_complete_please_pickup, size_check_error,
        weight_size_check_start, weight_size_check_complete, weight_size_check_error,
        set_item, clear_item, register_item,
        device_error_clear, device_error_weight, device_error_distance, device_error_rfid, 
        device_error_visoncam, device_error_lidar2d, device_error_lidar3d, device_error_indicator,
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