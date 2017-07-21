using Microsoft.VisualStudio.TestTools.UnitTesting;

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
            byte intention = MySerialPort.Communication.INTENTION_LOG;
            byte[] dataToRead = MySerialPort.Communication.prepareDataToRead(startAddr, dataLength, iMemory, intention);
            byte[] expectedDataToRead = {14, MySerialPort.Communication.READ_COMMAND, MySerialPort.Communication.EEPROM_MEMORY,
                MySerialPort.Communication.INTENTION_LOG,
                0x00, 0x00, 0x00,
                0x00, 0x00, 0x00,
                0xC6, 0xD5, 0x1B, 0x18};
            CollectionAssert.AreEqual(expectedDataToRead, dataToRead);
        }

        [TestMethod]
        public void TestPrepareDataToRead2()
        {
            int startAddr = 0;
            int dataLength = 3;
            int iMemory = 1;
            byte intention = MySerialPort.Communication.INTENTION_LOG;
            byte[] dataToRead = MySerialPort.Communication.prepareDataToRead(startAddr, dataLength, iMemory, intention);
            byte[] expectedDataToRead = {14, MySerialPort.Communication.READ_COMMAND, MySerialPort.Communication.EEPROM_MEMORY,
                MySerialPort.Communication.INTENTION_LOG,
                0x00, 0x00, 0x00,
                0x03, 0x00, 0x00,
                0xC4, 0x93, 0xA5, 0x41};
            CollectionAssert.AreEqual(expectedDataToRead, dataToRead);
        }

        [TestMethod]
        public void TestPrepareDataToRead3()
        {
            int startAddr = 8000004;
            int dataLength = 13;
            int iMemory = 1;
            byte intention = MySerialPort.Communication.INTENTION_LOG;
            byte[] dataToRead = MySerialPort.Communication.prepareDataToRead(startAddr, dataLength, iMemory, intention);
            byte[] expectedDataToRead = {14, MySerialPort.Communication.READ_COMMAND, MySerialPort.Communication.EEPROM_MEMORY,
                MySerialPort.Communication.INTENTION_LOG,
                0x04, 0x12, 0x7A,
                0x11, 0x12, 0x7A,
                0x96, 0x26, 0xFC, 0xA2};
            CollectionAssert.AreEqual(expectedDataToRead, dataToRead);
        }
    }
}
