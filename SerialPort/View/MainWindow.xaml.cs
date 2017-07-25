using System;
using System.IO.Ports;
using System.Windows;
using System.Windows.Threading;
using MySerialPort.Model;

namespace MySerialPort.View
{
    /// <summary>
    /// Communication with rail circuit card via serial port.
    /// Protocol reference: https://docs.google.com/document/d/1Nx-vx0cXp-aIgKuCpmPjQVIvB5yWJyjRDjy-y_SSSkc
    /// </summary>
    public partial class MainWindow : Window
    {
        private const string NEW_LINE = "\n";

        public MainWindow()
        {
            InitializeComponent();
            foreach (var s in SerialPort.GetPortNames())
            {
                AvailableSerialPorts.Items.Add(s);
            }
            AvailableSerialPorts.SelectedIndex = AvailableSerialPorts.Items.Count - 1;
            var dataTemp = Communication.GenerateRandomData(5);
            DataToSend.Text = Communication.ToHexString(dataTemp);
        }

        public delegate void UpdateUiTextDelegate(string text);

        private void WriteDataToUi(string text)
        {
            if (string.Equals(text, NEW_LINE))
            {
                DataReceived.Text += text;
            }
            else
            {
                DataReceived.Text += Communication.GetTimeStampedStr(RemoveDashes(text));
            }
        }

        private class SerialPortUpdate : ISerialPortUpdate
        {
            readonly MainWindow _mainWindow;

            public SerialPortUpdate(MainWindow window) => _mainWindow = window;

            public void Update(byte[] dataReceived)
            {
                _mainWindow.Dispatcher.Invoke(DispatcherPriority.Send, new UpdateUiTextDelegate(_mainWindow.WriteDataToUi),
                    BitConverter.ToString(dataReceived));
            }

            public void TransmissionEnd(string message)
            {
                _mainWindow.Dispatcher.Invoke(DispatcherPriority.Send, new UpdateUiTextDelegate(_mainWindow.WriteDataToUi),
                    NEW_LINE);
                MessageBox.Show(message);
            }

            public bool ContinueAfterTimeout(int t_ms, int iTimeout)
            {
                bool continueAfterTimeout;
                var result = MessageBox.Show(
                    $"Timeout limit of {t_ms} ms reached before transmission could end." +
                    $"\niTimeout = {iTimeout}\nDo you want to keep on waiting?"
                    , "Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.No)
                {
                    _mainWindow.DataReceived.Dispatcher.Invoke(new UpdateReceivedDataTextCallback(_mainWindow.UpdateReceivedDataText),
                        new object[] {$"ERROR: Timout at {t_ms} ms!"});
                    continueAfterTimeout = false;
                }
                else
                {
                    continueAfterTimeout = true;
                }
                return continueAfterTimeout;
            }
        }

        private void ConnectClick(object sender, RoutedEventArgs e)
        {
            var portName = AvailableSerialPorts.SelectedItem.ToString();
            try
            {
                Communication.OpenSerialPort(new SerialPort(portName, Communication.BaudRate, Parity.None, 8, StopBits.One),
                                new SerialPortUpdate(this));
                ConnectedTo.Text = "Connected to " + portName;
                ConnectionEnabled(true);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "ERROR: Could not open " + portName);
            }
        }

        private void ConnectionEnabled(bool isEnabled)
        {
            Connect.IsEnabled = !isEnabled;
            SendWrite.IsEnabled = isEnabled;
            SendRead.IsEnabled = isEnabled;
            CloseSerialPort.IsEnabled = isEnabled;
            DataReceived.Text = "";
            DataSent.Text = "";
        }

        private void SendWriteClick(object sender, RoutedEventArgs e)
        {
            var data = Communication.ParseData(DataToSend.Text);
            var allBytes = Communication.PrepareDataToWrite(System.Convert.ToInt32(StartAddr.Text),
                data, MemoryType.SelectedIndex, Communication.IntentionCommand);
            CommunicateWithSerialPort(allBytes, 0);

        }

        private void CommunicateWithSerialPort(byte[] allBytes, int dataLength)
        {
            if (Communication.IsSerialPortOk())
            {
                Communication.SendToSerialPort(allBytes, 0, allBytes.Length, dataLength);
                DataSent.Text = RemoveDashes(BitConverter.ToString(allBytes));
            }
            else
            {
                MessageBox.Show(Communication.GetSerialPortStatus());
            }
        }

        private string RemoveDashes(string text)
        {
            return Communication.HexStart + text.Replace("-", ", " + Communication.HexStart);
        }

        private void SendReadClick(object sender, RoutedEventArgs e)
        {
            var dataLength = System.Convert.ToInt32(DataLength.Text);
            var allBytes = Communication.PrepareDataToRead(System.Convert.ToInt32(StartAddr.Text),
                dataLength, MemoryType.SelectedIndex, Communication.IntentionCommand);
            CommunicateWithSerialPort(allBytes, dataLength);
        }

        public delegate void UpdateReceivedDataTextCallback(string message);

        public void UpdateReceivedDataText(string message)
        {
            DataReceived.Text = message;
        }

        private void CloseSerialPortClick(object sender, RoutedEventArgs e)
        {
            Communication.CloseSerialPort();
            ConnectionEnabled(false);
            ConnectedTo.Text = "Connection closed";
        }

        private void Convert_Click(object sender, RoutedEventArgs e)
        {
            ConvertedData.Text = Communication.ConvertHexToStr(DataReceived.Text);
        }

    }
}
