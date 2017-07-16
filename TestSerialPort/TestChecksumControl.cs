using System;
using System.Text;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TestSerialPort
{
    /// <summary>
    /// Unit tests of ChecksumControl class methods.
    /// </summary>
    [TestClass]
    public class TestChecksumControl
    {
        MySerialPort.Crc32 crc32 = new MySerialPort.Crc32();
        [TestMethod]
        public void TestIsChecksumOk1()
        {
            List<byte[]> receivedList = new List<byte[]>();
            receivedList.Add(new byte[] { 0x00, 0x00, 0x00, 0x00 });
            byte[] checksum = { 0x21, 0x44, 0xDF, 0x1C };
            receivedList.Add(checksum);
            bool isOK = MySerialPort.ChecksumControl.isChecksumOk(receivedList, crc32, 4);
            String str = String.Format("Received = {0}, Expected = {1}", 
                MySerialPort.ChecksumControl.getByteArrayAsString(MySerialPort.ChecksumControl.Received),
                MySerialPort.ChecksumControl.getByteArrayAsString(MySerialPort.ChecksumControl.Expected));
            Assert.IsTrue(isOK);
        }
    }
}
