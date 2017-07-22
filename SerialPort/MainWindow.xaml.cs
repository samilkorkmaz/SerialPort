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
        private byte expectedReceivedDataLength;
        private int totalBytesReceived;
        private bool isTranmissionEnded;
        private static readonly int TIMEOUT_LIMIT_MS = 2 * 1000;
        private static readonly int TIMEOUT_CHECK_INTERVAL_MS = 100;
        private List<byte[]> receivedBytesList;

        public MainWindow()
        {
            InitializeComponent();
            foreach (string s in SerialPort.GetPortNames())
            {
                AvailableSerialPorts.Items.Add(s);
            }
            AvailableSerialPorts.SelectedIndex = AvailableSerialPorts.Items.Count - 1;
            byte[] dataTemp = Communication.generateRandomData(5);
            DataToSend.Text = Communication.toHexString(dataTemp);
        }

        //Event handler is triggered/run on a NON-UI thread
        private void dataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            if (!isTranmissionEnded) //TODO do we need this if? If yes, then we need a mechanism to verify that data sending has stopped so that we can start the next command
            {                
                byte[] dataReceivedFromCard = Communication.readFromSerialPort();
                receivedBytesList.Add(dataReceivedFromCard);
                totalBytesReceived += dataReceivedFromCard.Length;
                String endStr = "";
                if (totalBytesReceived == expectedReceivedDataLength)
                {
                    endStr = Communication.END_OF_RECEIVED_CARD_DATA;
                    isTranmissionEnded = true;
                }
                if (totalBytesReceived > expectedReceivedDataLength)
                {
                    string msg = String.Format("totalBytesReceived ({0}) > expectedReceivedDataLength ({1})",
                        totalBytesReceived, expectedReceivedDataLength);
                    endStr = Communication.END_OF_RECEIVED_CARD_DATA;
                    isTranmissionEnded = true;
                    MessageBox.Show(msg);
                    //throw new ArgumentOutOfRangeException(msg);                
                }
                //Update UI:
                Dispatcher.Invoke(DispatcherPriority.Send, new UpdateUiTextDelegate(WriteDataToUI), BitConverter.ToString(dataReceivedFromCard) + endStr);
            }
        }

        private delegate void UpdateUiTextDelegate(string text);

        private void WriteDataToUI(string text)
        {
            DataReceived.Text = DataReceived.Text + Communication.getTimeStampedStr(text);
        }

        private void connectClick(object sender, RoutedEventArgs e)
        {
            String portName = AvailableSerialPorts.SelectedItem.ToString();
            try
            {
                Communication.openSerialPort(new SerialPort(portName, Communication.BAUD_RATE, Parity.None, 8, StopBits.One),
                    new SerialDataReceivedEventHandler(dataReceived));
                ConnectedTo.Text = "Connected to " + portName;
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
            SendRead.IsEnabled = isEnabled;
            CloseSerialPort.IsEnabled = isEnabled;
            DataReceived.Text = "";
            DataSent.Text = "";
        }

        private void sendWriteClick(object sender, RoutedEventArgs e)
        {
            byte[] dataSentBytes = Communication.prepareDataToWrite(System.Convert.ToInt32(StartAddr.Text),
                Communication.parseData(DataToSend.Text), MemoryType.SelectedIndex, Communication.INTENTION_COMMAND);
            DataSent.Text = "0x" + BitConverter.ToString(dataSentBytes).Replace("-", ", 0x");
            if (Communication.isSerialPortOk())
            {
                reset();
                expectedReceivedDataLength = 1 + 4;
                Communication.sendToSerialPort(dataSentBytes, 0, dataSentBytes.Length);
            }
            else
            {
                MessageBox.Show(Communication.getSerialPortStatus());
            }
        }

        private void sendReadClick(object sender, RoutedEventArgs e)
        {
            byte[] dataReadBytes = Communication.prepareDataToRead(System.Convert.ToInt32(StartAddr.Text),
                System.Convert.ToInt32(DataLength.Text), MemoryType.SelectedIndex, Communication.INTENTION_COMMAND);
            DataSent.Text = "0x" + BitConverter.ToString(dataReadBytes).Replace("-", ", 0x");
            if (Communication.isSerialPortOk())
            {
                reset();
                expectedReceivedDataLength = (byte)(1 + System.Convert.ToInt32(DataLength.Text) + 4);
                Communication.sendToSerialPort(dataReadBytes, 0, dataReadBytes.Length);
            }
            else
            {
                MessageBox.Show(Communication.getSerialPortStatus());
            }
        }

        private void reset()
        {
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

        public delegate void UpdateReceivedDataTextCallback(string message);

        private void UpdateReceivedDataText(string message)
        {
            DataReceived.Text = message;
        }

        private void closeSerialPortClick(object sender, RoutedEventArgs e)
        {
            Communication.closeSerialPort();
            connectionEnabled(false);
            ConnectedTo.Text = "Connection closed";
        }

        private void Convert_Click(object sender, RoutedEventArgs e)
        {
            ConvertedData.Text = Communication.convertHexToStr(DataReceived.Text);
        }

    }
}
