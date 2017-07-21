using System;
using System.Collections.Generic;

namespace MySerialPort
{
    public class ChecksumControl
    {
        private static byte[] expected;
        private static byte[] received;

        public static byte[] Expected { get => expected; }
        public static byte[] Received { get => received; }

        /// <summary>
        /// Convert byte array to hex string.
        /// </summary>
        /// <param name="array"></param>
        /// <returns></returns>
        public static String getByteArrayAsString(byte[] array)
        {
            String str = "";
            for (int i = 0; i < array.Length; i++)
            {
                str += System.Convert.ToString(array[i], Communication.HEX_BASE);
            }
            return str;
        }

        /// <summary>
        /// Compare checksum calculated using Crc32 with checksum in received data.
        /// </summary>
        /// <param name="receivedBytesList"></param>
        /// <param name="crc32"></param>
        /// <param name="checksumLength"></param>
        /// <returns></returns>
        public static bool isChecksumOk(List<byte[]> receivedBytesList, Crc32 crc32, int checksumLength)
        {
            int arraySize = 0;
            foreach (var bytes in receivedBytesList)
            {
                arraySize += bytes.Length;
            }
            if (arraySize <= checksumLength)
            {
                throw new ArgumentOutOfRangeException(String.Format("Byte array size {0} must be larger than checksumLength {1}", arraySize, checksumLength));
            }
            byte[] allReceivedBytes = new byte[arraySize];
            int iStart = 0;
            foreach (var bytes in receivedBytesList)
            {
                Array.Copy(bytes, 0, allReceivedBytes, iStart, bytes.Length);
                iStart += bytes.Length;
            }
            byte[] receivedBytesWithoutChecksum = new byte[arraySize - checksumLength];
            Array.Copy(allReceivedBytes, receivedBytesWithoutChecksum, arraySize - checksumLength);
            byte[] expectedCrc32 = crc32.ComputeHash(receivedBytesWithoutChecksum);
            byte[] receivedCrc32 = new byte[checksumLength];
            Array.Copy(allReceivedBytes, allReceivedBytes.Length - checksumLength, receivedCrc32, 0, checksumLength);
            expected = expectedCrc32;
            received = receivedCrc32;
            for (int i = 0; i < checksumLength; i++)
            {
                if (expectedCrc32[i] != receivedCrc32[i])
                {
                    return false;
                }
            }
            return true;
        }
    }
}
