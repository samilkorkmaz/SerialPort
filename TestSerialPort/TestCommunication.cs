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
            int startAddr = 0;
            int dataLength = 0;
            int iMemory = 1;
            byte intention = Communication.INTENTION_LOG;
            try
            {
                byte[] dataToRead = Communication.prepareDataToRead(startAddr, dataLength, iMemory, intention);
                Assert.IsTrue(false);            }
            catch (ArgumentOutOfRangeException)
            {
                Assert.IsTrue(true);
            }
        }

        [TestMethod]
        public void TestPrepareDataToRead2()
        {
            int startAddr = 0;
            int dataLength = 3;
            int iMemory = 1;
            byte intention = Communication.INTENTION_LOG;
            byte[] dataToRead = Communication.prepareDataToRead(startAddr, dataLength, iMemory, intention);
            byte[] expectedDataToRead = {14, Communication.READ_COMMAND, Communication.EEPROM_MEMORY,
                Communication.INTENTION_LOG,
                0x00, 0x00, 0x00,
                0x02, 0x00, 0x00,
                0xC5, 0x51, 0xCF, 0x76};
            CollectionAssert.AreEqual(expectedDataToRead, dataToRead);
        }

        [TestMethod]
        public void TestPrepareDataToRead3()
        {
            int startAddr = 8000004; //fills all 3 bytes reserved for lowerAddr
            int dataLength = 13;
            int iMemory = 1;
            byte intention = Communication.INTENTION_LOG;
            byte[] dataToRead = Communication.prepareDataToRead(startAddr, dataLength, iMemory, intention);
            byte[] expectedDataToRead = {14, Communication.READ_COMMAND, Communication.EEPROM_MEMORY,
                Communication.INTENTION_LOG,
                0x04, 0x12, 0x7A,
                0x10, 0x12, 0x7A,
                0x97, 0xE4, 0x96, 0x95};
            CollectionAssert.AreEqual(expectedDataToRead, dataToRead);
        }

        [TestMethod]
        public void TestPrepareDataToWrite1()
        {
            int startAddr = 8000004; //fills all 3 bytes reserved for lowerAddr
            int iMemory = 1;
            byte intention = Communication.INTENTION_LOG;
            byte[] data = {0xAB};
            byte[] dataToWrite = Communication.prepareDataToWrite(startAddr, data, iMemory, intention);
            byte[] expectedDataToWrite = {(byte)(14 + data.Length), Communication.WRITE_COMMAND, Communication.EEPROM_MEMORY,
                Communication.INTENTION_LOG,
                0x04, 0x12, 0x7A,
                0x04, 0x12, 0x7A,
                0xAB,
                0x19, 0x94, 0x97, 0x59};
            CollectionAssert.AreEqual(expectedDataToWrite, dataToWrite);
        }

        [TestMethod]
        public void TestPrepareDataToWrite2()
        {
            int startAddr = 8000004; //fills all 3 bytes reserved for lowerAddr
            int iMemory = 1;
            byte intention = Communication.INTENTION_LOG;
            byte[] data = { 0xAB, 0xCD, 0xEF, 0xFF };
            byte[] dataToWrite = Communication.prepareDataToWrite(startAddr, data, iMemory, intention);
            byte[] expectedDataToWrite = {(byte)(14 + data.Length), Communication.WRITE_COMMAND, Communication.EEPROM_MEMORY,
                Communication.INTENTION_LOG,
                0x04, 0x12, 0x7A,
                0x07, 0x12, 0x7A,
                0xAB, 0xCD, 0xEF, 0xFF,
                0xD8, 0x7D, 0xD4, 0xA3};
            CollectionAssert.AreEqual(expectedDataToWrite, dataToWrite);
        }

        [TestMethod]
        public void TestPrepareDataToWrite3()
        {
            int startAddr = 8000004; //fills all 3 bytes reserved for lowerAddr
            int iMemory = 1;
            byte intention = Communication.INTENTION_LOG;
            byte[] data = { };
            try
            {
                byte[] dataToWrite = Communication.prepareDataToWrite(startAddr, data, iMemory, intention);
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
            string str = Communication.removeTimeStamps("[12:13:56] samil");
            string expectedStr = " samil";
            Assert.AreEqual(expectedStr, str);
        }

        [TestMethod]
        public void TestRemoveTimeStamps2()
        {
            string str = Communication.removeTimeStamps("[12:13:56] samil\n[12:13:56:99] korkmaz");
            string expectedStr = " samil\n korkmaz";
            Assert.AreEqual(expectedStr, str);
        }
    }
}
