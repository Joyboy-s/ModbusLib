using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModbusLib
{
    /// <summary>
    /// 数据转换的一些操作
    /// </summary>
    public class DataConversion
    {
        /// <summary>
        /// 字符串转16进制byte
        /// </summary>
        /// <param name="TheString">要转换的字符串</param>
        /// <returns></returns>
        public byte[] StringToByte_0x(string TheString) {
            Encoding FromEncoding = Encoding.GetEncoding("UTF-8");
            Encoding ToEncoding = Encoding.GetEncoding("gb2312");
            byte[] fromBytes = FromEncoding.GetBytes(TheString);
            byte[] toBytes = Encoding.Convert(FromEncoding, ToEncoding, fromBytes);
            return toBytes;
        }

        /// <summary>
        /// 16进制Byte转string
        /// </summary>
        /// <param name="TheByte">要转String的byte</param>
        /// <returns></returns>
        public string ByteToString_0x(byte[] TheByte) {
            string MyString;
            Encoding FromEncoding = Encoding.GetEncoding("gb2312");
            Encoding ToEncoding = Encoding.GetEncoding("UTF-8");
            byte[] toBytes = Encoding.Convert(FromEncoding, ToEncoding, TheByte);
            MyString = ToEncoding.GetString(toBytes); ;
            return MyString;
        }
    }
}
