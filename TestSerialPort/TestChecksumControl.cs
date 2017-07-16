using System;
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
            Assert.IsTrue(isOK);
        }

        [TestMethod]
        public void TestIsChecksumOk2()
        {
            List<byte[]> receivedList = new List<byte[]>();
            receivedList.Add(new byte[] { 0xE9, 0x5C, 0xA8, 0xEE, 0xFF, 0x8D, 0xFA, 0xD9, 0xC4, 0xEA });
            byte[] checksum = { 0xBF, 0x54, 0xE3, 0x63 };
            receivedList.Add(checksum);
            bool isOK = MySerialPort.ChecksumControl.isChecksumOk(receivedList, crc32, 4);
            String str = String.Format("Received = {0}, Expected = {1}",
                MySerialPort.ChecksumControl.getByteArrayAsString(MySerialPort.ChecksumControl.Received),
                MySerialPort.ChecksumControl.getByteArrayAsString(MySerialPort.ChecksumControl.Expected));
            Assert.IsTrue(isOK);
        }

        [TestMethod]
        public void TestIsChecksumOk3()
        {
            try
            {
                List<byte[]> receivedList = new List<byte[]>();
                receivedList.Add(new byte[] { });
                byte[] checksum = { 0xBF, 0x54, 0xE3, 0x63 };
                receivedList.Add(checksum);
                bool isOK = MySerialPort.ChecksumControl.isChecksumOk(receivedList, crc32, 4);
                Assert.IsTrue(false);
            }
            catch (ArgumentOutOfRangeException e)
            {
                Assert.IsTrue(true);
            }
        }

        [TestMethod]
        public void TestIsChecksumOk4()
        {
            try
            {
                List<byte[]> receivedList = new List<byte[]>();
                receivedList.Add(new byte[] { });
                byte[] checksum = { };
                receivedList.Add(checksum);
                bool isOK = MySerialPort.ChecksumControl.isChecksumOk(receivedList, crc32, 4);
                Assert.IsTrue(false);
            }
            catch (ArgumentOutOfRangeException e)
            {
                Assert.IsTrue(true);
            }
        }
    }
}
