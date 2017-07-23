using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MySerialPort
{
    public interface ISerialPortUpdate
    {
        void update(byte[] dataReceived);
        void transmissionEnd(string message);
        bool ContinueAfterTimeout(int t_ms, int iTimeout);
    }
}
