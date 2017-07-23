using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MySerialPort.Model;

namespace TestSerialPort
{
    /// <summary>
    /// Unit tests of ChecksumControl class methods.
    /// </summary>
    [TestClass]
    public class TestChecksumControl
    {
        private readonly Crc32 _crc32 = new Crc32();

        [TestMethod]
        public void TestIsChecksumOk1()
        {
            var receivedList = new List<byte[]>();
            receivedList.Add(new byte[] { 0x00, 0x00, 0x00, 0x00 });
            byte[] checksum = { 0x21, 0x44, 0xDF, 0x1C };
            receivedList.Add(checksum);
            var isOk = ChecksumControl.IsChecksumOk(receivedList, _crc32, 4);
            Assert.IsTrue(isOk);
        }

        [TestMethod]
        public void TestIsChecksumOk2()
        {
            var receivedList = new List<byte[]>
            {
                new byte[] {0xE9, 0x5C, 0xA8, 0xEE, 0xFF, 0x8D, 0xFA, 0xD9, 0xC4, 0xEA}
            };
            byte[] checksum = { 0xBF, 0x54, 0xE3, 0x63 };
            receivedList.Add(checksum);
            var isOk = ChecksumControl.IsChecksumOk(receivedList, _crc32, 4);
            var unused =
                $"Received = {ChecksumControl.GetByteArrayAsString(ChecksumControl.Received)}, Expected = {ChecksumControl.GetByteArrayAsString(ChecksumControl.Expected)}";
            Assert.IsTrue(isOk);
        }

        [TestMethod]
        public void TestIsChecksumOk3()
        {
            try
            {
                var receivedList = new List<byte[]> {new byte[] { }};
                byte[] checksum = { 0xBF, 0x54, 0xE3, 0x63 };
                receivedList.Add(checksum);
                ChecksumControl.IsChecksumOk(receivedList, _crc32, 4);
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
                var receivedList = new List<byte[]> {new byte[] { }};
                byte[] checksum = { };
                receivedList.Add(checksum);
                ChecksumControl.IsChecksumOk(receivedList, _crc32, 4);
                Assert.IsTrue(false);
            }
            catch (ArgumentOutOfRangeException e)
            {
                Assert.IsTrue(true);
            }
        }
    }
}
