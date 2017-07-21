using System;
using System.Collections;
using System.IO.Ports;
using System.Windows;

namespace MySerialPort
{
    class Communication
    {
        public static readonly string END_OF_RECEIVED_CARD_DATA = "END OF RECEIVED CARD DATA";
        public static readonly int HEX_BASE = 16;
        private static SerialPort serialPort;
        private static Crc32 crc32 = new Crc32();

        public static readonly byte NEW_LINE = 10;
        public static readonly int BAUD_RATE = 38400; //4.69KBit/s
        private static readonly byte MAX_DATA_PACKET_LENGTH = 250;

        private static readonly byte READ_COMMAND = 0xAA;
        private static readonly byte WRITE_COMMAND = 0xDD;

        private static readonly byte SD_CARD_MEMORY = 0x1A;
        private static readonly byte EEPROM_MEMORY = 0x1B;
        private static readonly byte CPU_MEMORY = 0x1C;

        private static readonly byte INTENTION_COMMAND = 0x00; //TODO
        private static readonly byte HEADER_LENGTH = 9;
        public static readonly byte CHECKSUM_LENGTH = 4;
        private static readonly byte DATA_START_INDEX = 10;
        private static readonly byte LOW_ADDR_START_INDEX = 4;
        private static readonly byte HI_ADDR_START_INDEX = (byte)(LOW_ADDR_START_INDEX + 3);

        public static void sendToSerialPort(byte [] buffer, int offset, int count)
        {
            serialPort.Write(buffer, offset, count);
        }

        public static String readFromSerialPort()
        {
            return serialPort.ReadExisting();
        }

        public static Crc32 getCrc32()
        {
            return crc32;
        }

        public static bool isSerialPortOk()
        {
            if (serialPort == null)
            {
                MessageBox.Show("serialPort == null");
            }
            else if (!serialPort.IsOpen)
            {
                MessageBox.Show("!serialPort.IsOpen");
            }
            return serialPort != null && serialPort.IsOpen;
        }

        private static byte getDataPacketLength(int dataLength)
        {
            int dataPacketLengthInt = HEADER_LENGTH + dataLength + CHECKSUM_LENGTH + 1;
            if (dataPacketLengthInt > MAX_DATA_PACKET_LENGTH)
            {
                throw new Exception(String.Format("dataPacketLength {0} > MAX_DATA_PACKET_LENGTH {1}!", dataPacketLengthInt, MAX_DATA_PACKET_LENGTH));
            }
            byte dataPacketLength = (byte)(dataPacketLengthInt);
            return dataPacketLength;
        }

        public static byte[] prepareDataToSend(int startAddr, byte[] data, int iMemory)
        {
            byte dataPacketLength = getDataPacketLength(data.Length);
            byte[] dataSentBytes = new byte[dataPacketLength];
            dataSentBytes[0] = dataPacketLength;
            dataSentBytes[1] = WRITE_COMMAND;
            dataSentBytes[2] = getMemoryType(iMemory);
            dataSentBytes[3] = INTENTION_COMMAND;

            byte[] lowAddr = BitConverter.GetBytes(startAddr);
            dataSentBytes[LOW_ADDR_START_INDEX] = lowAddr[0];
            dataSentBytes[LOW_ADDR_START_INDEX + 1] = lowAddr[1];
            dataSentBytes[LOW_ADDR_START_INDEX + 2] = lowAddr[2];

            int hiAddrInt = BitConverter.ToInt32(lowAddr, 0) + data.Length - 1;
            byte[] hiAddrBytes = BitConverter.GetBytes(hiAddrInt);
            dataSentBytes[HI_ADDR_START_INDEX] = hiAddrBytes[0];
            dataSentBytes[HI_ADDR_START_INDEX + 1] = hiAddrBytes[1];
            dataSentBytes[HI_ADDR_START_INDEX + 2] = hiAddrBytes[2];

            for (int i = DATA_START_INDEX; i < DATA_START_INDEX + data.Length; i++)
            {
                dataSentBytes[i] = data[i - DATA_START_INDEX];
            }
            int iStartCRC = DATA_START_INDEX + data.Length;
            byte[] checksumCRC32 = crc32.ComputeHash(dataSentBytes, 0, iStartCRC);
            for (int i = iStartCRC; i < iStartCRC + CHECKSUM_LENGTH; i++)
            {
                dataSentBytes[i] = checksumCRC32[i - iStartCRC];
            }
            return dataSentBytes;
        }

        internal static void openSerialPort(SerialPort serialPort, SerialDataReceivedEventHandler serialDataReceivedEventHandler)
        {
            Communication.serialPort = serialPort;
            serialPort.Open();
            // Attach a method to be called when there is data waiting in the port's buffer
            serialPort.DataReceived += serialDataReceivedEventHandler;
        }

        internal static void closeSerialPort()
        {
            serialPort.Close();
        }
        public static String getTimeStampedStr(String str)
        {
            return "[" + DateTime.Now.ToLongTimeString() + "] " + str + "\n";
            //return str;
        }


        public static byte[] prepareDataToRead(int startAddr, int dataLength, int iMemory)
        {
            byte dataPacketLength = getDataPacketLength(0);
            byte[] dataSentBytes = new byte[dataPacketLength];
            dataSentBytes[0] = dataPacketLength;
            dataSentBytes[1] = READ_COMMAND;
            dataSentBytes[2] = getMemoryType(iMemory);
            dataSentBytes[3] = INTENTION_COMMAND;

            byte[] lowAddr = BitConverter.GetBytes(startAddr);
            dataSentBytes[LOW_ADDR_START_INDEX] = lowAddr[0];
            dataSentBytes[LOW_ADDR_START_INDEX + 1] = lowAddr[1];
            dataSentBytes[LOW_ADDR_START_INDEX + 2] = lowAddr[2];

            int hiAddrInt = BitConverter.ToInt32(lowAddr, 0) + dataLength - 1;
            byte[] hiAddrBytes = BitConverter.GetBytes(hiAddrInt);
            dataSentBytes[HI_ADDR_START_INDEX] = hiAddrBytes[0];
            dataSentBytes[HI_ADDR_START_INDEX + 1] = hiAddrBytes[1];
            dataSentBytes[HI_ADDR_START_INDEX + 2] = hiAddrBytes[2];

            int iStartCRC = DATA_START_INDEX; //no data is sent to card when reading from card
            byte[] checksumCRC32 = crc32.ComputeHash(dataSentBytes, 0, DATA_START_INDEX);
            for (int i = iStartCRC; i < iStartCRC + CHECKSUM_LENGTH; i++)
            {
                dataSentBytes[i] = checksumCRC32[i - iStartCRC];
            }
            return dataSentBytes;
        }


        private static byte getMemoryType(int iMemory)
        {
            switch (iMemory)
            {
                case 0:
                    return SD_CARD_MEMORY;
                case 1:
                    return EEPROM_MEMORY;
                case 2:
                    return CPU_MEMORY;
                default:
                    throw new Exception(String.Format("Uknown selection " + iMemory));
            }
        }

        public static byte[] parseData(String text)
        {
            string[] strings = text.Split(',');
            byte[] bytes = new byte[strings.Length];
            for (int i = 0; i < strings.Length; i++)
            {
                string s = strings[i];
                if (!String.IsNullOrEmpty(s.Trim()))
                {
                    bytes[i] = System.Convert.ToByte(s.Trim().Substring(2, 2), HEX_BASE); //Remove "0x" part
                }
                else
                {
                    throw new Exception(String.Format("String is null or empty: {0}", s));
                }
            }
            return bytes;
        }

        public static string toHexString(byte[] bytes)
        {
            string s = "";
            foreach (var b in bytes)
            {
                s = s + String.Format("0x{0}, ", System.Convert.ToString(b, Communication.HEX_BASE).ToUpper());
            }
            //remove last comma
            s = s.Substring(0, s.Length - 2);
            return s;
        }

        public static byte[] generateRandomData(int dataLength)
        {
            Random rnd = new Random();
            byte[] dataTemp = new byte[dataLength];
            for (int i = 0; i < dataLength; i++)
            {
                dataTemp[i] = (byte)rnd.Next(1, 0xFF);  // 1 <= dataTemp[i] < 255;
            }
            return dataTemp;
        }

        //https://stackoverflow.com/a/228060/51358
        public static string Reverse(string s)
        {
            char[] charArray = s.ToCharArray();
            Array.Reverse(charArray);
            return new string(charArray);
        }

        public static string convertHexToStr(string dataStr)
        {
            string convertedStr = "";
            try
            {
                int iEnd = dataStr.IndexOf(END_OF_RECEIVED_CARD_DATA);
                if (iEnd >= 0)
                {
                    dataStr = dataStr.Substring(0, dataStr.Length - END_OF_RECEIVED_CARD_DATA.Length);
                }
                //byte[] dataBytes = Encoding.ASCII.GetBytes(dataStr);
                byte[] dataBytes = Communication.parseData(dataStr);
                BitArray bits = new BitArray(dataBytes);
                //BitArray bits = new BitArray(new byte[1] { 1 });
                int iByte = 0;
                string bitStr = "";
                for (int counter = 0; counter < bits.Length; counter++)
                {
                    if (counter % 8 == 0)
                    {
                        if (dataBytes[iByte] == Communication.NEW_LINE)
                        {
                            convertedStr = convertedStr + "\\n" + ": " + dataBytes[iByte].ToString() + ": ";
                            iByte++;
                        }
                        else
                        {
                            convertedStr = convertedStr + System.Convert.ToString(dataBytes[iByte], Communication.HEX_BASE).ToUpper() + ": ";
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
                convertedStr = convertedStr + String.Format("\"{0}\" string is not in hex format, cannot convert!\n", dataStr);
            }
            return convertedStr;
        }
    }

}
