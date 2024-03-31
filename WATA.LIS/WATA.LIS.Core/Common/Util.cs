using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using WATA.LIS.Core.Model.DPS;
using static WATA.LIS.Core.Common.Tools;

namespace WATA.LIS.Core.Common
{
    public class Util
    {

        public static string GetStringLogRawData(byte[] HexData)
        {
            String strData = "";
            for (int i = 0; i < HexData.Length; i++)
            {
                strData += String.Format("0x{0:x2} ", HexData[i]);
            }
            return strData;
        }

        public static string BytesToString(byte[] HexData)
        {
            String strData = "";
            for (int i = 0; i < HexData.Length; i++)
            {
                //strData += String.Format("{0:x2} ", HexData[i]);
                strData += (Convert.ToChar(HexData[i])).ToString();
            }

            return strData;
        }

        public static int GetObjectSize(object obj)
        {
            return  Marshal.SizeOf(obj);
        }

        public static UInt16 BytesToBigEdianUint16(byte[] buffer, int offset)
        {
            byte[] tempbuffer = new byte[2];
            Array.Copy(buffer, offset, tempbuffer, 0, 2);
            Array.Reverse(tempbuffer);
            return BitConverter.ToUInt16(tempbuffer,0);
        }

        public static Int16 BytesToBigEdianInt16(byte[] buffer, int offset)
        {
            byte[] tempbuffer = new byte[2];
            Array.Copy(buffer, offset, tempbuffer, 0, 2);
            Array.Reverse(tempbuffer);
            return BitConverter.ToInt16(tempbuffer,0);
        }

        public static string DebugBytestoString(byte[] input)
        {
            string strRet="";
            for (int idx = 0; idx < input.Length; idx++)
            {
                strRet += String.Format("[{0:X2}]", input[idx]);
            }
            return strRet;
        }

        public static bool IsNumeric(string input)
        {
            int number = 0;
            return int.TryParse(input, out number);
        }



        public static Int32 BitArrayToInt32(BitArray bitarray)
        {
            int value = 0;

            for (int i = 0; i < bitarray.Count; i++)
            {
                if (bitarray[i])
                {

                    //value += Convert.ToInt32(Math.Pow(2, i));
                    value += (Int32)Convert.ToInt64(Math.Pow(2, i));
                }
                    
            }

            return value;
        }

        public static BitArray Int32toBitArray(Int32 input)
        {
            BitArray bitarray = new BitArray(BitConverter.GetBytes(input));

            return bitarray;
        }


        public static BitArray Int16toBitArray(UInt16 input)
        {
            BitArray bitarray = new BitArray(BitConverter.GetBytes(input));

            return bitarray;
        }


        public static byte[] ObjectToByte(object obj)
        {
            int size = Marshal.SizeOf(obj);
            byte[] arr = new byte[size];
            IntPtr ptr = Marshal.AllocHGlobal(size);
            Marshal.StructureToPtr(obj, ptr, false);
            Marshal.Copy(ptr, arr, 0, size);
            Marshal.FreeHGlobal(ptr);
            return arr;
        }

        
        public static byte[] SerializeObject(SetDisplayModel obj)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                BinaryFormatter formatter = new BinaryFormatter();
                formatter.Serialize(stream, obj);
                return stream.ToArray();
            }
        }

        public static T ByteToObject<T>(byte[] buffer) where T : class
        {
            int size = Marshal.SizeOf(typeof(T));
            if (size > buffer.Length)
            {
                throw new Exception();
            }

            IntPtr ptr = Marshal.AllocHGlobal(size);
            Marshal.Copy(buffer, 0, ptr, size);
            T obj = (T)Marshal.PtrToStructure(ptr, typeof(T));
            Marshal.FreeHGlobal(ptr);
            return obj;
        }

        public static string ObjectToJson(object obj)
        {
            try
            {
                string rst = JsonConvert.SerializeObject(obj);
                return rst;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Json Convert exception = {0}", ex.Message);
                return null;
            }

        }

        public static byte[] StringToByteArray(string json)
        {
            return Encoding.UTF8.GetBytes(json);
        }

        #region Time Set
        [DllImport("kernel32.dll")]
        private extern static void GetSystemTime(ref SYSTEMTIME lpSystemTime);

        [DllImport("kernel32.dll")]
        private extern static uint SetSystemTime(ref SYSTEMTIME lpSystemTime);

        public static void SetTime(DateTime time)
        {
            time = time.ToUniversalTime();
            // Call the native GetSystemTime method 
            // with the defined structure.
            SYSTEMTIME systime = new SYSTEMTIME();

            GetSystemTime(ref systime);

            systime.wYear = (ushort)time.Year;
            systime.wMonth = (ushort)time.Month;
            systime.wDay = (ushort)time.Day;
            systime.wHour = (ushort)time.Hour;
            systime.wMinute = (ushort)time.Minute;
            systime.wSecond = (ushort)time.Second;


            uint result = SetSystemTime(ref systime);

            if (result == 0)
            {
                Console.WriteLine("OK");
            }

        }
        #endregion


        public static byte[] ToByteArray(BitArray bits)
        {
            int numBytes = bits.Count / 8;
            if (bits.Count % 8 != 0) numBytes++;

            byte[] bytes = new byte[numBytes];
            bits.CopyTo(bytes, 0);

            //int numBytes = bits.Count / 8;
            //if (bits.Count % 8 != 0) numBytes++;

            //byte[] bytes = new byte[numBytes];
            //int byteindex = 0, bitindex = 0;
            //for(int i=0; i<bits.Count;i++)
            //{
            //    if (bits[i])
            //        bytes[byteindex] |= (byte)(1 <<(7 - bitindex));

            //    bitindex++;
            //    if(bitindex == 8)
            //    {
            //        bitindex = 0;
            //        byteindex++;
            //    }
            //}
            return bytes;
        }

    }
}
