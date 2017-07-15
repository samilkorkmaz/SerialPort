using System;
using System.Text;
using System.Windows;
using System.IO.Ports;
using System.Windows.Threading;
using System.Collections;

namespace MySerialPort
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        SerialPort serialPort;
        private static readonly byte NEW_LINE = 10;
        private static readonly int BAUD_RATE = 38400; //4.69KBit/s
        private static readonly int MAX_DATA_PACKET_LENGTH = 250;

        private static readonly byte READ_COMMAND = 0xAA;
        private static readonly byte WRITE_COMMAND = 0xDD;

        private static readonly byte SD_CARD_MEMORY = 0x1A;
        private static readonly byte EEPROM_MEMORY = 0x1B;
        private static readonly byte FLASH_MEMORY = 0x1C;
        private static readonly byte CPU_MEMORY = 0x1D;
        private readonly byte memoryToUse = EEPROM_MEMORY;

        private readonly byte INTENTION_COMMAND = 0x00;
        private readonly byte END_OF_TRANSMISSION = 0x60;
        private readonly int DATA_START_INDEX = 9;
        private readonly int DATA_LENGTH = 10;
        private readonly int HEADER_LENGTH = 8;
        private readonly byte[] lowAddr = { 0x01, 0x00, 0x00, 0x00 }; //first byte is least significant. Note that I have to make it 4 bytes for the ToInt32 below to work.
        private byte[] data;
        private byte[] receivedBytes;

        public MainWindow()
        {
            InitializeComponent();
            foreach (string s in SerialPort.GetPortNames())
            {
                this.AvailableSerialPorts.Items.Add(s);
            }
            this.AvailableSerialPorts.SelectedIndex = this.AvailableSerialPorts.Items.Count - 1;
            Random rnd = new Random();
            byte[] bytes = new byte[DATA_LENGTH];
            for (int i = 0; i < DATA_LENGTH; i++)
            {
                bytes[i] = (byte)rnd.Next(1, 256);  // 1 <= month < 0x60;
            }
            this.DataReceived.Text = BitConverter.ToString(bytes).Replace("-", ", 0x");
            Crc32 crc32 = new Crc32();
            String output = "";
            foreach (byte b in crc32.ComputeHash(bytes))
            {
                output += b.ToString("x2").ToLower();
            }
            this.DataSent.Text = output;
        }

        //Event handler is triggered/run on a NON-UI thread
        private void dataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            // All the incoming data in the port's buffer
            string dataReceived = serialPort.ReadExisting();
            receivedBytes = Encoding.ASCII.GetBytes(dataReceived);
            //Update UI:
            Dispatcher.Invoke(DispatcherPriority.Send, new UpdateUiTextDelegate(WriteData), BitConverter.ToString(receivedBytes));
        }

        private delegate void UpdateUiTextDelegate(string text);

        private void WriteData(string text)
        {
            this.DataReceived.Text = this.DataReceived.Text + getTimeStampedStr(text);
        }

        private void connectClick(object sender, RoutedEventArgs e)
        {
            String portName = this.AvailableSerialPorts.SelectedItem.ToString();
            try
            {
                serialPort = new SerialPort(portName, BAUD_RATE, Parity.None, 8, StopBits.One);
                serialPort.Open();
                this.ConnectedTo.Text = "Connected to " + portName;
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
            this.Connect.IsEnabled = !isEnabled;
            this.SendWrite.IsEnabled = isEnabled;
            this.Close.IsEnabled = isEnabled;
            this.DataReceived.Text = "";
            this.DataSent.Text = "";
        }

        private void sendWriteClick(object sender, RoutedEventArgs e)
        {
            Random rnd = new Random();
            //protocol reference: https://docs.google.com/document/d/1Nx-vx0cXp-aIgKuCpmPjQVIvB5yWJyjRDjy-y_SSSkc
            data = new byte[DATA_LENGTH];
            for (int i = 0; i < DATA_LENGTH; i++)
            {
                data[i] = (byte)rnd.Next(1, END_OF_TRANSMISSION);  // 1 <= month < 0x60;
            }
            byte[] dataSentBytes = prepareDataToSend(data);
            this.DataSent.Text = BitConverter.ToString(dataSentBytes);
            serialPort.Write(dataSentBytes, 0, dataSentBytes.Length);
        }

        private void sendReadClick(object sender, RoutedEventArgs e)
        {
            byte[] dataReadBytes = prepareDataToRead(DATA_LENGTH);
            this.DataSent.Text = BitConverter.ToString(dataReadBytes);
            serialPort.Write(dataReadBytes, 0, dataReadBytes.Length);
        }

        private byte[] prepareDataToSend(byte[] data)
        {
            byte[] checksumCRC32 = { 0x0A, 0x0B, 0x0C, 0x0D };
            int dataPacketLength = HEADER_LENGTH + data.Length + checksumCRC32.Length + 2;

            if (dataPacketLength > MAX_DATA_PACKET_LENGTH)
            {
                throw new Exception(String.Format("dataPacketLength {0} > MAX_DATA_PACKET_LENGTH {1}!", dataPacketLength, MAX_DATA_PACKET_LENGTH));
            }

            byte[] dataSentBytes = new byte[dataPacketLength];
            dataSentBytes[0] = WRITE_COMMAND;
            dataSentBytes[1] = memoryToUse;
            dataSentBytes[2] = INTENTION_COMMAND;

            dataSentBytes[3] = lowAddr[0];
            dataSentBytes[4] = lowAddr[1];
            dataSentBytes[5] = lowAddr[2];

            int hiAddrInt = BitConverter.ToInt32(lowAddr, 0) + data.Length - 1;
            byte[] hiAddrBytes = BitConverter.GetBytes(hiAddrInt);
            dataSentBytes[6] = hiAddrBytes[0];
            dataSentBytes[7] = hiAddrBytes[1];
            dataSentBytes[8] = hiAddrBytes[2];

            for (int i = DATA_START_INDEX; i < DATA_START_INDEX + data.Length; i++)
            {
                dataSentBytes[i] = data[i - DATA_START_INDEX];
            }
            int iStartCRC = DATA_START_INDEX + data.Length;
            for (int i = iStartCRC; i < iStartCRC + checksumCRC32.Length; i++)
            {
                dataSentBytes[i] = checksumCRC32[i - iStartCRC];
            }
            dataSentBytes[DATA_START_INDEX + data.Length + checksumCRC32.Length] = END_OF_TRANSMISSION;
            return dataSentBytes;
        }

        private byte[] prepareDataToRead(int dataLength)
        {
            byte[] checksumCRC32 = { 0x0A, 0x0B, 0x0C, 0x0D };
            int dataPacketLength = HEADER_LENGTH + 0 + checksumCRC32.Length + 2;

            if (dataPacketLength > MAX_DATA_PACKET_LENGTH)
            {
                throw new Exception(String.Format("dataPacketLength {0} > MAX_DATA_PACKET_LENGTH {1}!", dataPacketLength, MAX_DATA_PACKET_LENGTH));
            }

            byte[] dataSentBytes = new byte[dataPacketLength];
            dataSentBytes[0] = READ_COMMAND;
            dataSentBytes[1] = memoryToUse;
            dataSentBytes[2] = INTENTION_COMMAND;

            dataSentBytes[3] = lowAddr[0];
            dataSentBytes[4] = lowAddr[1];
            dataSentBytes[5] = lowAddr[2];

            //Array.Reverse(lowAddr); //Convert to big endian
            int hiAddrInt = BitConverter.ToInt32(lowAddr, 0) + dataLength - 1;
            byte[] hiAddrBytes = BitConverter.GetBytes(hiAddrInt);
            //Array.Reverse(hiAddrBytes); //convert to little endian
            dataSentBytes[6] = hiAddrBytes[0];
            dataSentBytes[7] = hiAddrBytes[1];
            dataSentBytes[8] = hiAddrBytes[2];

            int iStartCRC = DATA_START_INDEX;
            for (int i = iStartCRC; i < iStartCRC + checksumCRC32.Length; i++)
            {
                dataSentBytes[i] = checksumCRC32[i - iStartCRC];
            }
            dataSentBytes[DATA_START_INDEX + 0 + checksumCRC32.Length] = END_OF_TRANSMISSION;
            return dataSentBytes;
        }

        private void closeClick(object sender, RoutedEventArgs e)
        {
            serialPort.Close();
            connectionEnabled(false);
            this.ConnectedTo.Text = "Connection closed";
        }

        private String getTimeStampedStr(String str)
        {
            return "[" + DateTime.Now.ToLongTimeString() + "] " + str + "\n";
            //return str;
        }

        private void Convert_Click(object sender, RoutedEventArgs e)
        {
            String dataStr = this.DataReceived.Text;
            byte[] dataBytes = Encoding.ASCII.GetBytes(dataStr);
            BitArray bits = new BitArray(dataBytes);
            //BitArray bits = new BitArray(new byte[1] { 1 });
            this.convertedData.Text = "";
            int iByte = 0;
            String bitStr = "";
            for (int counter = 0; counter < bits.Length; counter++)
            {
                if (counter % 8 == 0)
                {
                    if (dataBytes[iByte] == NEW_LINE)
                    {
                        this.convertedData.Text = this.convertedData.Text + "\\n" + ": " + dataBytes[iByte].ToString() + ": ";
                        iByte++;
                    }
                    else
                    {
                        this.convertedData.Text = this.convertedData.Text + dataStr.Substring(iByte, 1) + ": " + dataBytes[iByte].ToString() + ": ";
                        iByte++;
                    }
                }
                bitStr = bitStr + (bits[counter] ? "1" : "0");
                if ((counter + 1) % 8 == 0) //plot each byte on a separate line
                {
                    this.convertedData.Text = this.convertedData.Text + Reverse(bitStr); //Reverse bitStr to put low order bits to the left of string
                    bitStr = "";
                    this.convertedData.Text = this.convertedData.Text + "\n";
                }
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
