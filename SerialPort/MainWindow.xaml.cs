using System;
using System.Text;
using System.Windows;
using System.IO.Ports;
using System.Windows.Threading;
using System.Collections;
using System.Threading;
using System.Collections.Generic;

namespace MySerialPort
{
    /// <summary>
    /// Communication with rail circuit card via serial port.
    /// Protocol reference: https://docs.google.com/document/d/1Nx-vx0cXp-aIgKuCpmPjQVIvB5yWJyjRDjy-y_SSSkc
    /// </summary>
    public partial class MainWindow : Window
    {
        SerialPort serialPort;
        private static readonly byte NEW_LINE = 10;
        private static readonly int BAUD_RATE = 38400; //4.69KBit/s
        private static readonly byte MAX_DATA_PACKET_LENGTH = 250;

        private static readonly byte READ_COMMAND = 0xAA;
        private static readonly byte WRITE_COMMAND = 0xDD;

        private static readonly byte SD_CARD_MEMORY = 0x1A;
        private static readonly byte EEPROM_MEMORY = 0x1B;
        private static readonly byte FLASH_MEMORY = 0x1C;
        private static readonly byte CPU_MEMORY = 0x1D;

        public static readonly byte HEX_BASE = 16;

        private readonly byte INTENTION_COMMAND = 0x00; //TODO
        private static readonly byte HEADER_LENGTH = 9;
        private static readonly byte CHECKSUM_LENGTH = 4;
        private static readonly byte DATA_START_INDEX = 10;
        private static readonly byte LOW_ADDR_START_INDEX = 4;
        private static readonly byte HI_ADDR_START_INDEX = (byte)(LOW_ADDR_START_INDEX + 3);
        private byte[] receivedBytes;
        private Crc32 crc32 = new Crc32();
        private byte expectedReceivedDataLength;
        private bool transmissionJustStarted;
        private int totalBytesReceived;
        private static readonly string END_OF_RECEIVED_CARD_DATA = "END OF RECEIVED CARD DATA";
        private bool isTranmissionEnded;
        private static readonly int TIMEOUT_LIMIT_MS = 2 * 1000;
        private static readonly int TIMEOUT_CHECK_INTERVAL_MS = 100;
        private List<byte[]> receivedBytesList;

        private byte getMemoryType()
        {
            switch (MemoryType.SelectedIndex)
            {
                case 0:
                    return SD_CARD_MEMORY;
                case 1:
                    return EEPROM_MEMORY;
                case 2:
                    return FLASH_MEMORY;
                case 3:
                    return CPU_MEMORY;
                default:
                    throw new Exception(String.Format("Uknown selection " + MemoryType.SelectedItem.ToString()));
            }
        }

        public MainWindow()
        {
            InitializeComponent();
            foreach (string s in SerialPort.GetPortNames())
            {
                AvailableSerialPorts.Items.Add(s);
            }
            AvailableSerialPorts.SelectedIndex = AvailableSerialPorts.Items.Count - 1;
            generateData(3);
        }

        private String toHexString(byte[] bytes)
        {
            String s = "";
            foreach (var b in bytes)
            {
                s = s + String.Format("0x{0}, ", System.Convert.ToString(b, HEX_BASE).ToUpper());
            }
            //remove last comma
            s = s.Substring(0, s.Length - 2);
            return s;
        }

        private void generateData(int dataLength)
        {
            Random rnd = new Random();
            byte[] dataTemp = new byte[dataLength];
            for (int i = 0; i < dataLength; i++)
            {
                dataTemp[i] = (byte)rnd.Next(1, 0xFF);  // 1 <= dataTemp[i] < 255;
            }
            DataToSend.Text = toHexString(dataTemp);
        }

        private byte[] parseData(String text)
        {
            String[] strings = text.Split(',');
            byte[] bytes = new byte[strings.Length];
            for (int i = 0; i < strings.Length; i++)
            {
                String s = strings[i];
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

        //Event handler is triggered/run on a NON-UI thread
        private void dataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            // All the incoming data in the port's buffer
            string dataReceived = serialPort.ReadExisting();
            receivedBytes = Encoding.ASCII.GetBytes(dataReceived);
            receivedBytesList.Add(receivedBytes);
            if (transmissionJustStarted)
            {
                expectedReceivedDataLength = receivedBytes[0];
                transmissionJustStarted = false;
            }
            totalBytesReceived += receivedBytes.Length;
            String endStr = "";
            if (totalBytesReceived == expectedReceivedDataLength)
            {
                endStr = END_OF_RECEIVED_CARD_DATA;
                isTranmissionEnded = true;
            }
            //Update UI:
            Dispatcher.Invoke(DispatcherPriority.Send, new UpdateUiTextDelegate(WriteData),
                BitConverter.ToString(receivedBytes) + endStr);
        }

        private delegate void UpdateUiTextDelegate(string text);

        private void WriteData(string text)
        {
            DataReceived.Text = DataReceived.Text + getTimeStampedStr(text);
        }

        private void connectClick(object sender, RoutedEventArgs e)
        {
            String portName = AvailableSerialPorts.SelectedItem.ToString();
            try
            {
                serialPort = new SerialPort(portName, BAUD_RATE, Parity.None, 8, StopBits.One);
                serialPort.Open();
                ConnectedTo.Text = "Connected to " + portName;
                // Attach a method to be called when there is data waiting in the port's buffer
                serialPort.DataReceived += new SerialDataReceivedEventHandler(dataReceived);
                connectionEnabled(true);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "ERROR: Could not open " + portName);
            }
        }

        private void connectionEnabled(bool isEnabled)
        {
            Connect.IsEnabled = !isEnabled;
            SendWrite.IsEnabled = isEnabled;
            CloseSerialPort.IsEnabled = isEnabled;
            DataReceived.Text = "";
            DataSent.Text = "";
        }

        private void sendWriteClick(object sender, RoutedEventArgs e)
        {
            byte[] dataSentBytes = prepareDataToSend(System.Convert.ToInt32(StartAddr.Text),
                parseData(DataToSend.Text));
            DataSent.Text = "0x" + BitConverter.ToString(dataSentBytes).Replace("-", ", 0x");
            if (isSerialPortOk())
            {
                reset();
                serialPort.Write(dataSentBytes, 0, dataSentBytes.Length);
            }
        }

        private bool isSerialPortOk()
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

        private void sendReadClick(object sender, RoutedEventArgs e)
        {
            byte[] dataReadBytes = prepareDataToRead(System.Convert.ToInt32(StartAddr.Text),
                System.Convert.ToInt32(DataLength.Text));
            DataSent.Text = "0x" + BitConverter.ToString(dataReadBytes).Replace("-", ", 0x");
            if (isSerialPortOk())
            {
                reset();
                serialPort.Write(dataReadBytes, 0, dataReadBytes.Length);
            }
        }

        private void reset()
        {
            transmissionJustStarted = true;
            totalBytesReceived = 0;
            isTranmissionEnded = false;
            receivedBytesList = new List<byte[]>();
            Thread t = new Thread(checkTimeout);
            t.Start();
        }

        private void checkTimeout()
        {
            int iTimeout = 1;
            int t_ms = 0;
            while (true)
            {
                if (t_ms > iTimeout * TIMEOUT_LIMIT_MS)
                {
                    MessageBoxResult result = MessageBox.Show(String.Format("Timeout limit of {0} ms reached before transmission could end."
                        + "\niTimeout = {1}\nDo you want to keep on waiting?", iTimeout * TIMEOUT_LIMIT_MS, iTimeout)
                        , "Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (result == MessageBoxResult.No)
                    {
                        DataReceived.Dispatcher.Invoke(new UpdateReceivedDataTextCallback(UpdateReceivedDataText),
                            new object[] { String.Format("ERROR: Timout at {0} ms!", iTimeout * TIMEOUT_LIMIT_MS) });
                        break;
                    }
                    else
                    {
                        iTimeout *= 2;
                    }
                }
                if (isTranmissionEnded)
                {
                    MessageBox.Show(String.Format("Transmission ended at t = {0} ms.", t_ms));
                    if (!ChecksumControl.isChecksumOk(receivedBytesList, crc32, CHECKSUM_LENGTH))
                    {
                        MessageBox.Show(String.Format("Received crc32 byte {0} not as expected {1}!",
                            ChecksumControl.getByteArrayAsString(ChecksumControl.Received),
                            ChecksumControl.getByteArrayAsString(ChecksumControl.Expected)));
                        DataReceived.Dispatcher.Invoke(new UpdateReceivedDataTextCallback(UpdateReceivedDataText),
                            new object[] { "ERROR: Checksum wrong!" });
                    }
                    break;
                }
                Thread.Sleep(TIMEOUT_CHECK_INTERVAL_MS);
                t_ms += TIMEOUT_CHECK_INTERVAL_MS;
            }
        }

        public delegate void UpdateReceivedDataTextCallback(string message);

        private void UpdateReceivedDataText(string message)
        {
            DataReceived.Text = message;
        }

        private byte getDataPacketLength(int dataLength)
        {
            int dataPacketLengthInt = HEADER_LENGTH + dataLength + CHECKSUM_LENGTH + 1;
            if (dataPacketLengthInt > MAX_DATA_PACKET_LENGTH)
            {
                throw new Exception(String.Format("dataPacketLength {0} > MAX_DATA_PACKET_LENGTH {1}!", dataPacketLengthInt, MAX_DATA_PACKET_LENGTH));
            }
            byte dataPacketLength = (byte)(dataPacketLengthInt);
            return dataPacketLength;
        }

        private byte[] prepareDataToSend(int startAddr, byte[] data)
        {
            byte dataPacketLength = getDataPacketLength(data.Length);
            byte[] dataSentBytes = new byte[dataPacketLength];
            dataSentBytes[0] = dataPacketLength;
            dataSentBytes[1] = WRITE_COMMAND;
            dataSentBytes[2] = getMemoryType();
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

        private byte[] prepareDataToRead(int startAddr, int dataLength)
        {
            byte dataPacketLength = getDataPacketLength(0);
            byte[] dataSentBytes = new byte[dataPacketLength];
            dataSentBytes[0] = dataPacketLength;
            dataSentBytes[1] = READ_COMMAND;
            dataSentBytes[2] = getMemoryType();
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

        private void closeSerialPortClick(object sender, RoutedEventArgs e)
        {
            serialPort.Close();
            connectionEnabled(false);
            ConnectedTo.Text = "Connection closed";
        }

        private String getTimeStampedStr(String str)
        {
            return "[" + DateTime.Now.ToLongTimeString() + "] " + str + "\n";
            //return str;
        }

        private void Convert_Click(object sender, RoutedEventArgs e)
        {
            String dataStr = DataReceived.Text;
            try
            {
                int iEnd = dataStr.IndexOf(END_OF_RECEIVED_CARD_DATA);
                if (iEnd >= 0)
                {
                    dataStr = dataStr.Substring(0, dataStr.Length - END_OF_RECEIVED_CARD_DATA.Length);
                }
                //byte[] dataBytes = Encoding.ASCII.GetBytes(dataStr);
                byte[] dataBytes = parseData(dataStr);
                BitArray bits = new BitArray(dataBytes);
                //BitArray bits = new BitArray(new byte[1] { 1 });
                ConvertedData.Text = "";
                int iByte = 0;
                String bitStr = "";
                for (int counter = 0; counter < bits.Length; counter++)
                {
                    if (counter % 8 == 0)
                    {
                        if (dataBytes[iByte] == NEW_LINE)
                        {
                            ConvertedData.Text = ConvertedData.Text + "\\n" + ": " + dataBytes[iByte].ToString() + ": ";
                            iByte++;
                        }
                        else
                        {
                            ConvertedData.Text = ConvertedData.Text + System.Convert.ToString(dataBytes[iByte], HEX_BASE).ToUpper() + ": ";
                            iByte++;
                        }
                    }
                    bitStr = bitStr + (bits[counter] ? "1" : "0");
                    if ((counter + 1) % 8 == 0) //plot each byte on a separate line
                    {
                        ConvertedData.Text = ConvertedData.Text + Reverse(bitStr); //Reverse bitStr to put low order bits to the left of string
                        bitStr = "";
                        ConvertedData.Text = ConvertedData.Text + "\n";
                    }
                }
            }
            catch (Exception)
            {
                ConvertedData.Text = ConvertedData.Text + String.Format("\"{0}\" string is not in hex format, cannot convert!\n", dataStr);
            }
        }

        //https://stackoverflow.com/a/228060/51358
        public static string Reverse(string s)
        {
            char[] charArray = s.ToCharArray();
            Array.Reverse(charArray);
            return new string(charArray);
        }

    }
}
