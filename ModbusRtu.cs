using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ModbusLib
{
    /// <summary>
    /// 串口操作类
    /// </summary>
    public class ModbusRtu
    {
        #region 变量
        /// <summary>
        /// 串口操作对象
        /// </summary>
        public SerialPort CurrentCom;//串口操作对象
        #endregion
        #region 属性
        /// <summary>
        /// 设置超时时间2秒
        /// </summary>
        public int RcvTimeout { get; set; } = 2000;
        #endregion
        /// <summary>
        /// 构造方法初始化
        /// </summary>
        public ModbusRtu()
        {
            CurrentCom = new SerialPort();
        }

        /// <summary>
        /// 打开串口链接
        /// </summary>
        /// <param name="portName">设置通信端口</param>
        /// <param name="baudRate">设置串行波特率</param>
        /// <param name="parity">设置奇偶校验检查协议</param>
        /// <param name="dataBits">设置每个字节的标准数据位长度</param>
        /// <param name="stopBits">设置每个字节的标准停止位数</param>
        public void openCom(string portName, int baudRate, Parity parity, int dataBits, StopBits stopBits)
        {
            CurrentCom.PortName = portName;//端口号
            CurrentCom.BaudRate = baudRate;//设置串行波特率
            CurrentCom.Parity = parity;//设置奇偶校验检查协议
            CurrentCom.DataBits = dataBits;//设置每个字节的标准数据位长度
            CurrentCom.StopBits = stopBits;//设置每个字节的标准停止位数
            if (CurrentCom.IsOpen)
            {
                CurrentCom.Close();
            }
            CurrentCom.Open();//打开串口

        }

        /// <summary>
        /// 关闭串口
        /// </summary>
        public void closeCom()
        {
            if (CurrentCom.IsOpen)
            {
                CurrentCom.Close();
            }
        }
        /// <summary>
        ///读取线离散量0x01
        /// </summary>
        /// <param name="SlaveId">从站地址</param>
        /// <param name="startAddress">起始地址</param>
        /// <param name="count">请求数量</param>
        /// <returns></returns>
        public byte[] sendCommand_01(byte SlaveId, ushort startAddress, ushort count)
        {
            #region 拼接报文

            List<byte> sendCom = new List<byte>();
            //从站地址+功能码+起始（高）+起始（低）+数量（高）+数量（低）+校验(CRC)
            sendCom.Add(SlaveId);
            sendCom.Add(0x01);
            sendCom.Add((byte)(startAddress / 256));//高位
            sendCom.Add((byte)(startAddress % 256));//低位
            sendCom.Add((byte)(count / 256));//高位
            sendCom.Add((byte)(count % 256));//低位
            byte[] crc = HslCommunication.Serial.SoftCRC16.CRC16(sendCom.ToArray());
            #endregion
            Thread.Sleep(60);
            #region 发送报文
            CurrentCom.Write(crc, 0, crc.Length);
            Thread.Sleep(60);
            #endregion

            #region 接收报文
            byte[] buffer = new byte[CurrentCom.BytesToRead];
            byte[] response = null;
            if (SendAndSaveByte(buffer, ref response))
            {
                //验证报文
                if (HslCommunication.Serial.SoftCRC16.CheckCRC16(response))
                {
                    if (response[0] == SlaveId)
                    {
                        return GetByteByIndex(response, 3, response.Length - 5);
                    }

                }
            }
            #endregion

            return null;

        }
        /// <summary>
        /// 接收的数据保存到内存缓存中
        /// </summary>
        /// <param name="buffer">用于接收缓冲区的数据</param>
        /// <param name="response">用于接收内存存储接收的缓冲区数据</param>
        /// <returns></returns>
        public bool SendAndSaveByte(byte[] buffer, ref byte[] response)
        {
            try
            {
                //创建一个内存空间用来接收传过来的数据
                MemoryStream ms = new MemoryStream();
                //记录请求开始发送的时间
                DateTime start = DateTime.Now;
                while (true)
                {
                    Thread.Sleep(20);//发送可能会有延迟，线程等待20毫秒
                                     //判断读的缓冲区是否有值
                    if (CurrentCom.BytesToRead > 0)
                    {
                        int counts = CurrentCom.Read(buffer, 0, buffer.Length);
                        ms.Write(buffer, 0, counts);
                    }
                    else
                    {
                        //判断是否超时
                        if ((DateTime.Now - start).TotalMilliseconds > this.RcvTimeout)
                        {
                            ms.Dispose();
                            return false;

                        }
                        else
                        {
                            //如果缓冲区已经有值跳出
                            if (ms.Length > 0)
                            {
                                break;
                            }
                        }
                    }
                }
                response = ms.ToArray();
                ms.Dispose();//释放资源
                return true;
            }
            catch (Exception)
            {

                return false;

            }


        }
        /// <summary>
        /// 截取byte获取想要的数据
        /// </summary>
        /// <param name="buff">要截取的字节</param>
        /// <param name="start">开始的位置</param>
        /// <param name="count">截取多少个字节</param>
        /// <returns></returns>
        public byte[] GetByteByIndex(byte[] buff, int start, int count)
        {
            if (buff == null || buff.Length <= 0)
            {
                return null;
            }
            if (start < 0 || count < 0)
            {
                return null;
            }
            if (buff.Length <= start + count)
            {
                return null;
            }
            byte[] reult = new byte[count];
            Array.Copy(buff, start, reult, 0, count);
            return reult;




        }

        #region 功能码0x0F写多个线圈
        /// <summary>
        /// 发送0x0F命令请求修改多个线状态返回请求结果
        /// </summary>
        /// <param name="SlaveId">从站地址</param>
        /// <param name="startAddress">开始修改的位置</param>
        /// <param name="registercount">要修改的寄存器数量</param>
        /// <param name="Number">发送的数据集合</param>
        /// <param name="msg">错误返回消息</param>
        /// <returns></returns>
        public bool sendCommand_0F(byte SlaveId, ushort startAddress, ushort registercount, byte[] Number, ref string msg)
        {

            List<byte> sendCom = new List<byte>();
            //从站地址+功能码+起始位高低+寄存器数高低位+字节数+变更数据位高低+CRC差错校验
            sendCom.Add(SlaveId);//从站地址
            sendCom.Add(0x0F);//功能码
            sendCom.Add((byte)(startAddress / 255));//起始位高
            sendCom.Add((byte)(startAddress % 255));//起始位低
            sendCom.Add((byte)(registercount / 255));//寄存器数高位
            sendCom.Add((byte)(registercount % 255));//寄存器数低位
            sendCom.Add((byte)(Number.Length));//字节数
            //变更数据高低位
            for (int i = 0; i < Number.Length; i++)
            {
                sendCom.Add(Number[i]);
            }
            byte[] crc = HslCommunication.Serial.SoftCRC16.CRC16(sendCom.ToArray());//使用方法进行CRC校验
            //发送报文
            Thread.Sleep(60);
            CurrentCom.Write(crc, 0, crc.Length);
            Thread.Sleep(60);

            //接收报文
            byte[] buffer = new byte[CurrentCom.BytesToRead];//设置集合用于接收缓冲区内容，长度为缓冲区字节的长度
            byte[] response = null;//定义空字符集合用于验证回发的报文正确性
            if (SendAndSaveByte(buffer, ref response))
            {
                //CRC验证回发报文
                if (HslCommunication.Serial.SoftCRC16.CheckCRC16(response))
                {
                    //判断返回的id是否与之前的相同
                    if (response[0] == SlaveId)
                    {

                        byte[] res = GetByteByIndex(response, 1, response.Length - 3);
                        try
                        {
                            if (res[0]==0x0F)
                            {
                                return true;
                            }
                            else
                            {
                                msg = "返回报文的指令与发送指令不一致";
                                return false;
                            }
                        }
                        catch (Exception)
                        {
                            msg = "返回报文有误";
                            return false;
                        }
                    }

                }
                else
                {
                    msg = "返回报文无法通过CRC验证";
                    return false;
                }
            }
            else
            {
                msg = "读取返回报文失败!";
                return false;
            }
       
            return false;
        }
        #endregion

        #region 功能码0x05写单个线圈
        /// <summary>
        /// 发送0x05命令请求修改单个线状态返回请求结果
        /// </summary>
        /// <param name="SlaveId">从站ID</param>
        /// <param name="startAddress">要修改的位置</param>
        /// <param name="MSB_Number">修改的值高位</param>
        /// <param name="LSB_Number">修改的值低位</param>
        /// <param name="msg">请求错误的具体信息</param>
        /// <returns></returns>
        public bool sendCommand_05(byte SlaveId, ushort startAddress, byte MSB_Number, byte LSB_Number,ref string msg)
        {

            List<byte> sendCom = new List<byte>();
            //从站地址+功能码+起始位高低+变更数据位高低+CRC差错校验
            sendCom.Add(SlaveId);//从站地址
            sendCom.Add(0x05);//功能码
            sendCom.Add((byte)(startAddress / 255));//起始位高
            sendCom.Add((byte)(startAddress % 255));//起始位低
            sendCom.Add(MSB_Number);//数据位高
            sendCom.Add(LSB_Number);//数据位低
            byte[] crc = HslCommunication.Serial.SoftCRC16.CRC16(sendCom.ToArray());//使用方法进行CRC校验
            //发送报文
            Thread.Sleep(60);
            CurrentCom.Write(crc, 0, crc.Length);
            Thread.Sleep(60);

            //接收报文
            byte[] buffer = new byte[CurrentCom.BytesToRead];//设置集合用于接收缓冲区内容，长度为缓冲区字节的长度
            byte[] response = null;//定义空字符集合用于验证回发的报文正确性
            try
            {
                //创建一个内存空间用来接收传过来的数据
                MemoryStream ms = new MemoryStream();
                //记录请求开始发送的时间
                DateTime start = DateTime.Now;
                while (true)
                {
                    Thread.Sleep(20);//发送可能会有延迟，线程等待20毫秒
                                     //判断读的缓冲区是否有值
                    if (CurrentCom.BytesToRead > 0)
                    {
                        int counts = CurrentCom.Read(buffer, 0, buffer.Length);
                        ms.Write(buffer, 0, counts);
                    }
                    else
                    {
                        //判断是否超时
                        if ((DateTime.Now - start).TotalMilliseconds > 2000)
                        {
                            ms.Dispose();
                            msg = "发送请求成功。接收报文超时！无法判断是否完成操作";
                            return false;

                        }
                        else
                        {
                            //如果缓冲区已经有值跳出
                            if (ms.Length > 0)
                            {
                                break;
                            }
                        }
                    }
                }
                response = ms.ToArray();//将内存中的数据写入集合
                ms.Dispose();//释放资源
                //CRC验证回发报文
                if (HslCommunication.Serial.SoftCRC16.CheckCRC16(response))
                {
                    //判断返回的id是否与之前的相同
                    if (response[0] == SlaveId)
                    {

                        byte[] res = GetByteByIndex(response, 1, response.Length - 3);
                        try
                        {
                            if (res[0] == 0x05 && res[1] == (byte)(startAddress / 255) && res[2] == (byte)(startAddress % 255) && res[3] == MSB_Number && res[4] == LSB_Number)
                            {
                                return true;
                            }
                            else
                            {
                                msg = "操作成功，接收报文内容与请求数据不符";
                                return false;
                            }
                        }
                        catch (Exception)
                        {
                            msg = "返回报文有误";
                            return false;
                        }
                    }

                }
                else
                {
                    msg = "返回报文无法通过CRC验证";
                    return false;
                }
            }
            catch (Exception)
            {
                msg = "发送报文成功，接收返回报文失败";
                return false;

            }
            return false;
        }
        #endregion

        #region 串口扫描
        /// <summary>
        /// 扫描串口方法
        /// </summary>
        /// <param name="MyPort">串口类用来调试扫描到的串口是否能打开</param>
        /// <param name="MyBox">用于显示串口的，下拉框</param>
        public void ShaoMiaoChuanKou(SerialPort MyPort, ComboBox MyBox)
        {
            MyBox.Items.Clear();//清空下拉框
            string[] serial_name = SerialPort.GetPortNames();
            ArrayList list = new ArrayList(serial_name);
            if (serial_name.Length > 0)
            {
                //遍历尝试打开串口，打开失败的将移除列表
                for (int i = 0; i < list.Count; i++)
                {
                    try
                    {
                        MyPort.PortName = list[i].ToString();
                        MyPort.Open();
                        MyPort.Close();
                    }
                    catch (Exception)
                    {
                        if (MyPort.IsOpen)
                        {
                            MyPort.Close();
                        }
                        list.Remove(list[i]);
                    }

                }
            }
            if (list.Count > 0)
            {
                Array.Sort(list.ToArray());
                MyBox.Items.AddRange(list.ToArray());
            }
            if (MyBox.Items.Count != 0)
            {
                MyBox.SelectedIndex = 0;
            }
        }
        #endregion
    }
}