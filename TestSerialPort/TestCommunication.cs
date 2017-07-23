using Microsoft.VisualStudio.TestTools.UnitTesting;
using MySerialPort.Model;
using System;

namespace TestSerialPort
{
    [TestClass]
    public class TestCommunication
    {
        [TestMethod]
        public void TestPrepareDataToRead1()
        {
            var startAddr = 0;
            var dataLength = 0;
            var iMemory = 1;
            var intention = Communication.IntentionLog;
            try
            {
                var unused = Communication.PrepareDataToRead(startAddr, dataLength, iMemory, intention);
                Assert.IsTrue(false);            }
            catch (ArgumentOutOfRangeException)
            {
                Assert.IsTrue(true);
            }
        }

        [TestMethod]
        public void TestPrepareDataToRead2()
        {
            var startAddr = 0;
            var dataLength = 3;
            var iMemory = 1;
            var intention = Communication.IntentionLog;
            var dataToRead = Communication.PrepareDataToRead(startAddr, dataLength, iMemory, intention);
            byte[] expectedDataToRead = {14, Communication.ReadCommand, Communication.EepromMemory,
                Communication.IntentionLog,
                0x00, 0x00, 0x00,
                0x02, 0x00, 0x00,
                0xC5, 0x51, 0xCF, 0x76};
            CollectionAssert.AreEqual(expectedDataToRead, dataToRead);
        }

        [TestMethod]
        public void TestPrepareDataToRead3()
        {
            var startAddr = 8000004; //fills all 3 bytes reserved for lowerAddr
            var dataLength = 13;
            var iMemory = 1;
            var intention = Communication.IntentionLog;
            var dataToRead = Communication.PrepareDataToRead(startAddr, dataLength, iMemory, intention);
            byte[] expectedDataToRead = {14, Communication.ReadCommand, Communication.EepromMemory,
                Communication.IntentionLog,
                0x04, 0x12, 0x7A,
                0x10, 0x12, 0x7A,
                0x97, 0xE4, 0x96, 0x95};
            CollectionAssert.AreEqual(expectedDataToRead, dataToRead);
        }

        [TestMethod]
        public void TestPrepareDataToWrite1()
        {
            var startAddr = 8000004; //fills all 3 bytes reserved for lowerAddr
            var iMemory = 1;
            var intention = Communication.IntentionLog;
            byte[] data = {0xAB};
            var dataToWrite = Communication.PrepareDataToWrite(startAddr, data, iMemory, intention);
            byte[] expectedDataToWrite = {(byte)(14 + data.Length), Communication.WriteCommand, Communication.EepromMemory,
                Communication.IntentionLog,
                0x04, 0x12, 0x7A,
                0x04, 0x12, 0x7A,
                0xAB,
                0x19, 0x94, 0x97, 0x59};
            CollectionAssert.AreEqual(expectedDataToWrite, dataToWrite);
        }

        [TestMethod]
        public void TestPrepareDataToWrite2()
        {
            var startAddr = 8000004; //fills all 3 bytes reserved for lowerAddr
            var iMemory = 1;
            var intention = Communication.IntentionLog;
            byte[] data = { 0xAB, 0xCD, 0xEF, 0xFF };
            var dataToWrite = Communication.PrepareDataToWrite(startAddr, data, iMemory, intention);
            byte[] expectedDataToWrite = {(byte)(14 + data.Length), Communication.WriteCommand, Communication.EepromMemory,
                Communication.IntentionLog,
                0x04, 0x12, 0x7A,
                0x07, 0x12, 0x7A,
                0xAB, 0xCD, 0xEF, 0xFF,
                0xD8, 0x7D, 0xD4, 0xA3};
            CollectionAssert.AreEqual(expectedDataToWrite, dataToWrite);
        }

        [TestMethod]
        public void TestPrepareDataToWrite3()
        {
            var startAddr = 8000004; //fills all 3 bytes reserved for lowerAddr
            var iMemory = 1;
            var intention = Communication.IntentionLog;
            byte[] data = { };
            try
            {
                var dataToWrite = Communication.PrepareDataToWrite(startAddr, data, iMemory, intention);
                Assert.IsTrue(false);
            }
            catch (ArgumentOutOfRangeException)
            {
                Assert.IsTrue(true);
            }
        }

        [TestMethod]
        public void TestRemoveTimeStamps1()
        {
            var str = Communication.RemoveTimeStamps("[12:13:56] samil");
            var expectedStr = " samil";
            Assert.AreEqual(expectedStr, str);
        }

        [TestMethod]
        public void TestRemoveTimeStamps2()
        {
            var str = Communication.RemoveTimeStamps("[12:13:56] samil\n[12:13:56:99] korkmaz");
            var expectedStr = " samil\n korkmaz";
            Assert.AreEqual(expectedStr, str);
        }
    }
}
