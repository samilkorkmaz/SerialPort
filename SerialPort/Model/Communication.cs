using System;
using System.Collections;
using System.IO.Ports;
using System.Threading;

namespace MySerialPort.Model
{
    public class Communication
    {
        public static readonly string HexStart = "0x";

        private static int _expectedTotalBytesReceived;
        private static int _totalBytesReceived;
        private static bool _isTranmissionEnded;
        private static ISerialPortUpdate _serialPortUpdate;

        public static readonly int HexBase = 16;
        private static SerialPort _serialPort;
        private static readonly Crc32 _crc32 = new Crc32();

        public static readonly byte NewLine = 10;
        public static readonly int BaudRate = 38400; //4.69KBit/s
        private static readonly byte MaxDataPacketLength = 250;

        public static readonly byte ReadCommand = 0xAA;
        public static readonly byte WriteCommand = 0xDD;

        public static readonly byte SdCardMemory = 0x1A;
        public static readonly byte EepromMemory = 0x1B;
        public static readonly byte CpuMemory = 0x1C;

        public static readonly byte IntentionLog = 0x2A;
        public static readonly byte IntentionCommand = 0x2B;
        public static readonly byte IntentionConfig = 0x2C;
        private static readonly byte HeaderLength = 9;
        public static readonly byte ChecksumLength = 4;
        private static readonly byte DataStartIndex = 10;
        private static readonly byte LowAddrStartIndex = 4;
        private static readonly byte HiAddrStartIndex = (byte)(LowAddrStartIndex + 3);

        private static readonly int TimeoutLimitMs = 2 * 1000;
        private static readonly int TimeoutCheckIntervalMs = 100;


        private static void Reset()
        {
            _totalBytesReceived = 0;
            _isTranmissionEnded = false;
            var t = new Thread(CheckTimeout);
            t.Start();
        }

        private static void CheckTimeout()
        {
            var iTimeout = 1;
            var tMs = 0;
            while (true)
            {
                if (tMs > iTimeout * TimeoutLimitMs)
                {
                    var continueAfterTimeout = _serialPortUpdate.ContinueAfterTimeout(iTimeout * TimeoutLimitMs, iTimeout);
                    if (continueAfterTimeout)
                    {
                        iTimeout *= 2;
                    }
                    else
                    {
                        break;
                    }
                }
                if (_isTranmissionEnded)
                {
                    _serialPortUpdate.TransmissionEnd($"Transmission ended at t = {tMs} ms.");
                    /*if (!ChecksumControl.isChecksumOk(receivedBytesList, Communication.getCrc32(), Communication.CHECKSUM_LENGTH))
                    {
                        MessageBox.Show(String.Format("Received crc32 byte {0} not as expected {1}!",
                            ChecksumControl.getByteArrayAsString(ChecksumControl.Received),
                            ChecksumControl.getByteArrayAsString(ChecksumControl.Expected)));
                        DataReceived.Dispatcher.Invoke(new UpdateReceivedDataTextCallback(UpdateReceivedDataText),
                            new object[] { "ERROR: Checksum wrong!" });
                    }*/
                    break;
                }
                Thread.Sleep(TimeoutCheckIntervalMs);
                tMs += TimeoutCheckIntervalMs;
            }
        }

        public static void SendToSerialPort(byte[] buffer, int offset, int count, int dataLength)
        {
            Reset();
            _expectedTotalBytesReceived = 1 + dataLength + ChecksumLength;
            _serialPort.Write(buffer, offset, count);
        }

        public static byte[] ReadFromSerialPort()
        {
            var bytesReceivedFromCard = new byte[_serialPort.BytesToRead];
            _serialPort.Read(bytesReceivedFromCard, 0, bytesReceivedFromCard.Length);
            return bytesReceivedFromCard;
        }

        public static Crc32 GetCrc32()
        {
            return _crc32;
        }

        public static bool IsSerialPortOk()
        {
            return _serialPort != null && _serialPort.IsOpen;
        }

        public static string GetSerialPortStatus()
        {
            if (_serialPort == null)
                return "serialPort == null";
            return !_serialPort.IsOpen ? "!serialPort.IsOpen" : "OK";
        }

        private static byte GetDataPacketLength(int dataLength)
        {
            var dataPacketLengthInt = HeaderLength + dataLength + ChecksumLength + 1;
            if (dataPacketLengthInt > MaxDataPacketLength)
            {
                throw new Exception(
                    $"dataPacketLength {dataPacketLengthInt} > MAX_DATA_PACKET_LENGTH {MaxDataPacketLength}!");
            }
            var dataPacketLength = (byte)(dataPacketLengthInt);
            return dataPacketLength;
        }

        public static byte[] PrepareDataToWrite(int startAddr, byte[] data, int iMemory, byte intention)
        {
            if (data.Length < 1)
            {
                throw new ArgumentOutOfRangeException($"data.Length {data.Length} cannot be less than 1!");
            }
            var dataPacketLength = GetDataPacketLength(data.Length);
            var dataSentBytes = new byte[dataPacketLength];
            dataSentBytes[0] = dataPacketLength;
            dataSentBytes[1] = WriteCommand;
            dataSentBytes[2] = GetMemoryType(iMemory);
            dataSentBytes[3] = intention;

            var lowAddr = BitConverter.GetBytes(startAddr);
            dataSentBytes[LowAddrStartIndex] = lowAddr[0];
            dataSentBytes[LowAddrStartIndex + 1] = lowAddr[1];
            dataSentBytes[LowAddrStartIndex + 2] = lowAddr[2];

            var hiAddrInt = BitConverter.ToInt32(lowAddr, 0) + data.Length - 1;
            var hiAddrBytes = BitConverter.GetBytes(hiAddrInt);
            dataSentBytes[HiAddrStartIndex] = hiAddrBytes[0];
            dataSentBytes[HiAddrStartIndex + 1] = hiAddrBytes[1];
            dataSentBytes[HiAddrStartIndex + 2] = hiAddrBytes[2];

            for (int i = DataStartIndex; i < DataStartIndex + data.Length; i++)
            {
                dataSentBytes[i] = data[i - DataStartIndex];
            }
            var iStartCrc = DataStartIndex + data.Length;
            var checksumCrc32 = _crc32.ComputeHash(dataSentBytes, 0, iStartCrc);
            for (var i = iStartCrc; i < iStartCrc + ChecksumLength; i++)
            {
                dataSentBytes[i] = checksumCrc32[i - iStartCrc];
            }
            return dataSentBytes;
        }

        public static void OpenSerialPort(SerialPort serialPort, ISerialPortUpdate serialPortUpdate)
        {
            _serialPort = serialPort;
            serialPort.Open();
            // Attach a method to be called when there is data waiting in the port's buffer
            _serialPortUpdate = serialPortUpdate;
            serialPort.DataReceived += DataReceived;
        }

        //Event handler is triggered/run on a NON-UI thread
        private static void DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            if (!_isTranmissionEnded) //TODO do we need this if? If yes, then we need a mechanism to verify that data sending has stopped so that we can start the next command
            {
                var bytesReceivedFromCard = ReadFromSerialPort();
                _totalBytesReceived += bytesReceivedFromCard.Length;
                if (_totalBytesReceived == _expectedTotalBytesReceived)
                {
                    _isTranmissionEnded = true;
                }
                if (_totalBytesReceived > _expectedTotalBytesReceived)
                {
                    var msg =
                        $"totalBytesReceived ({_totalBytesReceived}) > expectedReceivedDataLength ({_expectedTotalBytesReceived})";
                    _isTranmissionEnded = true;
                    _serialPortUpdate.TransmissionEnd(msg);
                    //throw new ArgumentOutOfRangeException(msg);                
                }
                //Update UI:
                _serialPortUpdate.Update(bytesReceivedFromCard);
            }
        }

        internal static void CloseSerialPort()
        {
            _serialPort.Close();
        }
        public static string GetTimeStampedStr(string str)
        {
            return "[" + DateTime.Now.ToLongTimeString() + "] " + str + "\n";
            //return str;
        }


        public static byte[] PrepareDataToRead(int startAddr, int dataLength, int iMemory, byte intention)
        {
            if (dataLength < 1)
            {
                throw new ArgumentOutOfRangeException($"dataLength {dataLength} cannot be less than 1!");
            }
            var dataPacketLength = GetDataPacketLength(0);
            var dataSentBytes = new byte[dataPacketLength];
            dataSentBytes[0] = dataPacketLength;
            dataSentBytes[1] = ReadCommand;
            dataSentBytes[2] = GetMemoryType(iMemory);
            dataSentBytes[3] = intention;

            var lowAddr = BitConverter.GetBytes(startAddr);
            dataSentBytes[LowAddrStartIndex] = lowAddr[0];
            dataSentBytes[LowAddrStartIndex + 1] = lowAddr[1];
            dataSentBytes[LowAddrStartIndex + 2] = lowAddr[2];

            var hiAddrInt = BitConverter.ToInt32(lowAddr, 0) + dataLength - 1;
            var hiAddrBytes = BitConverter.GetBytes(hiAddrInt);
            dataSentBytes[HiAddrStartIndex] = hiAddrBytes[0];
            dataSentBytes[HiAddrStartIndex + 1] = hiAddrBytes[1];
            dataSentBytes[HiAddrStartIndex + 2] = hiAddrBytes[2];

            int iStartCrc = DataStartIndex; //no data is sent to card when reading from card
            var checksumCrc32 = _crc32.ComputeHash(dataSentBytes, 0, DataStartIndex);
            for (var i = iStartCrc; i < iStartCrc + ChecksumLength; i++)
            {
                dataSentBytes[i] = checksumCrc32[i - iStartCrc];
            }
            return dataSentBytes;
        }


        private static byte GetMemoryType(int iMemory)
        {
            switch (iMemory)
            {
                case 0:
                    return SdCardMemory;
                case 1:
                    return EepromMemory;
                case 2:
                    return CpuMemory;
                default:
                    throw new Exception(string.Format("Uknown selection " + iMemory));
            }
        }

        public static byte[] ParseData(string text)
        {
            var strings = text.Split(',');
            var bytes = new byte[strings.Length];
            for (var i = 0; i < strings.Length; i++)
            {
                var s = strings[i];
                if (!string.IsNullOrEmpty(s.Trim()))
                {
                    bytes[i] = Convert.ToByte(s.Trim().Substring(2, 2), HexBase); //Remove "0x" part
                }
                else
                {
                    throw new Exception($"String is null or empty: {s}");
                }
            }
            return bytes;
        }

        public static string ToHexString(byte[] bytes)
        {
            var s = "";
            foreach (var b in bytes)
            {
                s += string.Format(HexStart + "{0}, ", Convert.ToString(b, HexBase).ToUpper());
            }
            //remove last comma
            s = s.Substring(0, s.Length - 2);
            return s;
        }

        public static byte[] GenerateRandomData(int dataLength)
        {
            var rnd = new Random();
            var dataTemp = new byte[dataLength];
            for (var i = 0; i < dataLength; i++)
            {
                dataTemp[i] = (byte)rnd.Next(1, 0xFF);  // 1 <= dataTemp[i] < 255;
            }
            return dataTemp;
            //return new byte[] { 0x35, 0x44, 0x15, 0xC1, 0xD0 };
        }

        //https://stackoverflow.com/a/228060/51358
        public static string Reverse(string s)
        {
            var charArray = s.ToCharArray();
            Array.Reverse(charArray);
            return new string(charArray);
        }

        public static string ConvertHexToStr(string dataStr)
        {
            var convertedStr = "";
            var timeStampsRemovedStr = RemoveTimeStamps(dataStr);
            try
            {
                var dataBytes = ParseData(timeStampsRemovedStr);
                var bits = new BitArray(dataBytes);
                var iByte = 0;
                var bitStr = "";
                for (var counter = 0; counter < bits.Length; counter++)
                {
                    if (counter % 8 == 0)
                    {
                        if (dataBytes[iByte] == NewLine)
                        {
                            convertedStr = convertedStr + "\\n" + ": " + dataBytes[iByte].ToString() + ": ";
                            iByte++;
                        }
                        else
                        {
                            convertedStr = convertedStr + Convert.ToString(dataBytes[iByte], HexBase).ToUpper() + ": ";
                            iByte++;
                        }
                    }
                    bitStr = bitStr + (bits[counter] ? "1" : "0");
                    if ((counter + 1) % 8 == 0) //plot each byte on a separate line
                    {
                        convertedStr = convertedStr + Reverse(bitStr); //Reverse bitStr to put low order bits to the left of string
                        bitStr = "";
                        convertedStr = convertedStr + "\n";
                    }
                }
            }
            catch (Exception)
            {
                convertedStr = convertedStr +
                               $"\"{timeStampsRemovedStr}\" string is not in hex format, cannot convert!\n";
            }
            return convertedStr;
        }

        public static string RemoveTimeStamps(string dataStr)
        {
            var strWithoutTimeStamps = dataStr;
            while (true)
            {
                var iStart = strWithoutTimeStamps.IndexOf("[");
                var iEnd = strWithoutTimeStamps.IndexOf("]");
                if (iStart > -1 && iEnd > -1)
                {
                    strWithoutTimeStamps = strWithoutTimeStamps.Remove(iStart, iEnd - iStart + 1);
                }
                else
                {
                    break;
                }
            }
            return strWithoutTimeStamps;
        }

    }

}
