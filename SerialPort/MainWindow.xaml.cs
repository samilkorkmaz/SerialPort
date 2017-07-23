using System;
using System.Windows;
using System.IO.Ports;
using System.Windows.Threading;

namespace MySerialPort
{
    /// <summary>
    /// Communication with rail circuit card via serial port.
    /// Protocol reference: https://docs.google.com/document/d/1Nx-vx0cXp-aIgKuCpmPjQVIvB5yWJyjRDjy-y_SSSkc
    /// </summary>
    public partial class MainWindow : Window
    {
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

        public delegate void UpdateUiTextDelegate(string text);

        private void WriteDataToUI(string text)
        {
            DataReceived.Text = DataReceived.Text + Communication.getTimeStampedStr(removeDashes(text));
        }

        private class SerialPortUpdate : ISerialPortUpdate
        {
            MainWindow mainWindow;

            public SerialPortUpdate(MainWindow window)
            {
                this.mainWindow = window;
            }

            public void update(byte[] dataReceived)
            {
                mainWindow.Dispatcher.Invoke(DispatcherPriority.Send, new UpdateUiTextDelegate(mainWindow.WriteDataToUI),
                    BitConverter.ToString(dataReceived));
            }

            public void transmissionEnd(string message)
            {
                MessageBox.Show(message);
            }

            public bool ContinueAfterTimeout(int t_ms, int iTimeout)
            {
                bool continueAfterTimeout;
                MessageBoxResult result = MessageBox.Show(String.Format("Timeout limit of {0} ms reached before transmission could end."
                        + "\niTimeout = {1}\nDo you want to keep on waiting?", t_ms, iTimeout)
                        , "Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.No)
                {
                    mainWindow.DataReceived.Dispatcher.Invoke(new UpdateReceivedDataTextCallback(mainWindow.UpdateReceivedDataText),
                        new object[] { String.Format("ERROR: Timout at {0} ms!", t_ms) });
                    continueAfterTimeout = false;
                }
                else
                {
                    continueAfterTimeout = true;
                }
                return continueAfterTimeout;
            }
        }

        private void connectClick(object sender, RoutedEventArgs e)
        {
            String portName = AvailableSerialPorts.SelectedItem.ToString();
            try
            {
                Communication.openSerialPort(new SerialPort(portName, Communication.BAUD_RATE, Parity.None, 8, StopBits.One),
                                new SerialPortUpdate(this));
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
            byte[] data = Communication.parseData(DataToSend.Text);
            byte[] allBytes = Communication.prepareDataToWrite(System.Convert.ToInt32(StartAddr.Text),
                data, MemoryType.SelectedIndex, Communication.INTENTION_COMMAND);
            communicateWithSerialPort(allBytes, data.Length);

        }

        private void communicateWithSerialPort(byte[] allBytes, int dataLength)
        {
            if (Communication.isSerialPortOk())
            {
                Communication.sendToSerialPort(allBytes, 0, allBytes.Length, dataLength);
                DataSent.Text = removeDashes(BitConverter.ToString(allBytes));
            }
            else
            {
                MessageBox.Show(Communication.getSerialPortStatus());
            }
        }

        private string removeDashes(string text)
        {
            return Communication.HEX_START + text.Replace("-", ", " + Communication.HEX_START);
        }

        private void sendReadClick(object sender, RoutedEventArgs e)
        {
            byte[] allBytes = Communication.prepareDataToRead(System.Convert.ToInt32(StartAddr.Text),
                System.Convert.ToInt32(DataLength.Text), MemoryType.SelectedIndex, Communication.INTENTION_COMMAND);
            communicateWithSerialPort(allBytes, 0);
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
