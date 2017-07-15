using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TestSerialPort
{
    [TestClass]
    public class TestCRC32
    {
        /// <summary>
        /// CRC32 reference values generated using http://www.sunshine2k.de/coding/javascript/crc/crc_js.html
        /// </summary>
        MySerialPort.Crc32 crc32 = new MySerialPort.Crc32();

        [TestMethod]
        public void TestComputeHash1()
        {
            byte[] bytes = { 0x00, 0x00, 0x00 };
            byte[] actual = crc32.ComputeHash(bytes);
            byte[] expected = { 0xFF, 0x41, 0xD9, 0x12 };
            CollectionAssert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void TestComputeHash2()
        {
            byte[] bytes = { 0x00, 0x01, 0x02 };
            byte[] actual = crc32.ComputeHash(bytes);
            byte[] expected = { 0x08, 0x54, 0x89, 0x7F };
            CollectionAssert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void TestComputeHash3()
        {
            byte[] bytes = {};
            byte[] actual = crc32.ComputeHash(bytes);
            byte[] expected = { 0x00, 0x00, 0x00, 0x00 };
            CollectionAssert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void TestComputeHash4()
        {
            try
            {
                byte[] bytes = null;
                byte[] actual = crc32.ComputeHash(bytes);
                Assert.IsTrue(false);
            }
            catch (System.ArgumentNullException e)
            {
                Assert.IsTrue(true);
            }            
        }

        [TestMethod]
        public void TestComputeHash5()
        {
            byte[] bytes = { 0xE9, 0x5C, 0xA8, 0xEE, 0xFF, 0x8D, 0xFA, 0xD9, 0xC4, 0xEA };
            byte[] actual = crc32.ComputeHash(bytes);
            byte[] expected = { 0xBF, 0x54, 0xE3, 0x63 };
            CollectionAssert.AreEqual(expected, actual);
        }
    }
}
