using System;
using System.Collections;
using System.Collections.Generic;
using System.IO.Ports;
using System.Threading;

namespace MySerialPort
{
    public class Communication
    {
        public static readonly string HEX_START = "0x";

        private static int expectedTotalBytesReceived;
        private static int totalBytesReceived;
        private static bool isTranmissionEnded;
        private static List<byte[]> receivedBytesList;
        private static ISerialPortUpdate serialPortUpdate;

        public static readonly int HEX_BASE = 16;
        private static SerialPort serialPort;
        private static Crc32 crc32 = new Crc32();

        public static readonly byte NEW_LINE = 10;
        public static readonly int BAUD_RATE = 38400; //4.69KBit/s
        private static readonly byte MAX_DATA_PACKET_LENGTH = 250;

        public static readonly byte READ_COMMAND = 0xAA;
        public static readonly byte WRITE_COMMAND = 0xDD;

        public static readonly byte SD_CARD_MEMORY = 0x1A;
        public static readonly byte EEPROM_MEMORY = 0x1B;
        public static readonly byte CPU_MEMORY = 0x1C;

        public static readonly byte INTENTION_LOG = 0x2A;
        public static readonly byte INTENTION_COMMAND = 0x2B;
        public static readonly byte INTENTION_CONFIG = 0x2C;
        private static readonly byte HEADER_LENGTH = 9;
        public static readonly byte CHECKSUM_LENGTH = 4;
        private static readonly byte DATA_START_INDEX = 10;
        private static readonly byte LOW_ADDR_START_INDEX = 4;
        private static readonly byte HI_ADDR_START_INDEX = (byte)(LOW_ADDR_START_INDEX + 3);

        private static readonly int TIMEOUT_LIMIT_MS = 2 * 1000;
        private static readonly int TIMEOUT_CHECK_INTERVAL_MS = 100;


        private static void reset()
        {
            totalBytesReceived = 0;
            isTranmissionEnded = false;
            receivedBytesList = new List<byte[]>();
            var t = new Thread(checkTimeout);
            t.Start();
        }

        private static void checkTimeout()
        {
            int iTimeout = 1;
            int t_ms = 0;
            while (true)
            {
                if (t_ms > iTimeout * TIMEOUT_LIMIT_MS)
                {
                    bool continueAfterTimeout = serialPortUpdate.ContinueAfterTimeout(iTimeout * TIMEOUT_LIMIT_MS, iTimeout);
                    if (continueAfterTimeout)
                    {
                        iTimeout *= 2;
                    }
                    else
                    {
                        break;
                    }
                }
                if (isTranmissionEnded)
                {
                    serialPortUpdate.transmissionEnd(String.Format("Transmission ended at t = {0} ms.", t_ms));
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
                Thread.Sleep(TIMEOUT_CHECK_INTERVAL_MS);
                t_ms += TIMEOUT_CHECK_INTERVAL_MS;
            }
        }

        public static void sendToSerialPort(byte[] buffer, int offset, int count, int dataLength)
        {
            reset();
            expectedTotalBytesReceived = 1 + dataLength + CHECKSUM_LENGTH;
            serialPort.Write(buffer, offset, count);
        }

        public static byte[] readFromSerialPort()
        {
            var bytesReceivedFromCard = new byte[serialPort.BytesToRead];
            serialPort.Read(bytesReceivedFromCard, 0, bytesReceivedFromCard.Length);
            return bytesReceivedFromCard;
        }

        public static Crc32 getCrc32()
        {
            return crc32;
        }

        public static bool isSerialPortOk()
        {
            return serialPort != null && serialPort.IsOpen;
        }

        public static string getSerialPortStatus()
        {
            if (serialPort == null)
            {
                return "serialPort == null";
            }
            else if (!serialPort.IsOpen)
            {
                return "!serialPort.IsOpen";
            }
            else
            {
                return "OK";
            }
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

        public static byte[] prepareDataToWrite(int startAddr, byte[] data, int iMemory, byte intention)
        {
            if (data.Length < 1)
            {
                throw new ArgumentOutOfRangeException(String.Format("data.Length %d cannot be less than 1!", data.Length));
            }
            byte dataPacketLength = getDataPacketLength(data.Length);
            var dataSentBytes = new byte[dataPacketLength];
            dataSentBytes[0] = dataPacketLength;
            dataSentBytes[1] = WRITE_COMMAND;
            dataSentBytes[2] = getMemoryType(iMemory);
            dataSentBytes[3] = intention;

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

        public static void openSerialPort(SerialPort serialPort, ISerialPortUpdate serialPortUpdate)
        {
            Communication.serialPort = serialPort;
            serialPort.Open();
            // Attach a method to be called when there is data waiting in the port's buffer
            Communication.serialPortUpdate = serialPortUpdate;
            serialPort.DataReceived += dataReceived;
        }

        //Event handler is triggered/run on a NON-UI thread
        private static void dataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            if (!isTranmissionEnded) //TODO do we need this if? If yes, then we need a mechanism to verify that data sending has stopped so that we can start the next command
            {
                byte[] bytesReceivedFromCard = Communication.readFromSerialPort();
                receivedBytesList.Add(bytesReceivedFromCard);
                totalBytesReceived += bytesReceivedFromCard.Length;
                if (totalBytesReceived == expectedTotalBytesReceived)
                {
                    isTranmissionEnded = true;
                }
                if (totalBytesReceived > expectedTotalBytesReceived)
                {
                    string msg = String.Format("totalBytesReceived ({0}) > expectedReceivedDataLength ({1})",
                        totalBytesReceived, expectedTotalBytesReceived);
                    isTranmissionEnded = true;
                    serialPortUpdate.transmissionEnd(msg);
                    //throw new ArgumentOutOfRangeException(msg);                
                }
                //Update UI:
                serialPortUpdate.update(bytesReceivedFromCard);
            }
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


        public static byte[] prepareDataToRead(int startAddr, int dataLength, int iMemory, byte intention)
        {
            if (dataLength < 1)
            {
                throw new ArgumentOutOfRangeException(String.Format("dataLength %d cannot be less than 1!", dataLength));
            }
            byte dataPacketLength = getDataPacketLength(0);
            byte[] dataSentBytes = new byte[dataPacketLength];
            dataSentBytes[0] = dataPacketLength;
            dataSentBytes[1] = READ_COMMAND;
            dataSentBytes[2] = getMemoryType(iMemory);
            dataSentBytes[3] = intention;

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
            var strings = text.Split(',');
            var bytes = new byte[strings.Length];
            for (int i = 0; i < strings.Length; i++)
            {
                var s = strings[i];
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
            var s = "";
            foreach (var b in bytes)
            {
                s += String.Format(HEX_START + "{0}, ", System.Convert.ToString(b, Communication.HEX_BASE).ToUpper());
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
            //return new byte[] { 0x35, 0x44, 0x15, 0xC1, 0xD0 };
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
            string timeStampsRemovedStr = removeTimeStamps(dataStr);
            try
            {
                byte[] dataBytes = parseData(timeStampsRemovedStr);
                BitArray bits = new BitArray(dataBytes);
                int iByte = 0;
                string bitStr = "";
                for (int counter = 0; counter < bits.Length; counter++)
                {
                    if (counter % 8 == 0)
                    {
                        if (dataBytes[iByte] == NEW_LINE)
                        {
                            convertedStr = convertedStr + "\\n" + ": " + dataBytes[iByte].ToString() + ": ";
                            iByte++;
                        }
                        else
                        {
                            convertedStr = convertedStr + System.Convert.ToString(dataBytes[iByte], HEX_BASE).ToUpper() + ": ";
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
                convertedStr = convertedStr + String.Format("\"{0}\" string is not in hex format, cannot convert!\n", timeStampsRemovedStr);
            }
            return convertedStr;
        }

        public static string removeTimeStamps(string dataStr)
        {
            string strWithoutTimeStamps = dataStr;
            while (true)
            {
                int iStart = strWithoutTimeStamps.IndexOf("[");
                int iEnd = strWithoutTimeStamps.IndexOf("]");
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
