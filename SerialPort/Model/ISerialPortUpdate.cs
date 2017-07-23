namespace MySerialPort.Model
{
    public interface ISerialPortUpdate
    {
        void update(byte[] dataReceived);
        void transmissionEnd(string message);
        bool ContinueAfterTimeout(int t_ms, int iTimeout);
    }
}
