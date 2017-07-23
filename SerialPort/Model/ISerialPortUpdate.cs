namespace MySerialPort.Model
{
    public interface ISerialPortUpdate
    {
        void Update(byte[] dataReceived);
        void TransmissionEnd(string message);
        bool ContinueAfterTimeout(int t_ms, int iTimeout);
    }
}
