using System;
using System.Collections.Generic;
using System.Linq;

namespace MySerialPort.Model
{
    public class ChecksumControl
    {
        public static byte[] Expected { get; private set; }
        public static byte[] Received { get; private set; }

        /// <summary>
        /// Convert byte array to hex string.
        /// </summary>
        /// <param name="array"></param>
        /// <returns></returns>
        public static string GetByteArrayAsString(byte[] array)
        {
            return array.Aggregate("", (current, t) => current + System.Convert.ToString(t, Communication.HexBase));
        }

        /// <summary>
        /// Compare checksum calculated using Crc32 with checksum in received data.
        /// </summary>
        /// <param name="receivedBytesList"></param>
        /// <param name="crc32"></param>
        /// <param name="checksumLength"></param>
        /// <returns></returns>
        public static bool IsChecksumOk(List<byte[]> receivedBytesList, Crc32 crc32, int checksumLength)
        {
            var arraySize = receivedBytesList.Sum(bytes => bytes.Length);
            if (arraySize <= checksumLength)
            {
                throw new ArgumentOutOfRangeException(
                    $"Byte array size {arraySize} must be larger than checksumLength {checksumLength}");
            }
            var allReceivedBytes = new byte[arraySize];
            var iStart = 0;
            foreach (var bytes in receivedBytesList)
            {
                Array.Copy(bytes, 0, allReceivedBytes, iStart, bytes.Length);
                iStart += bytes.Length;
            }
            var receivedBytesWithoutChecksum = new byte[arraySize - checksumLength];
            Array.Copy(allReceivedBytes, receivedBytesWithoutChecksum, arraySize - checksumLength);
            var expectedCrc32 = crc32.ComputeHash(receivedBytesWithoutChecksum);
            var receivedCrc32 = new byte[checksumLength];
            Array.Copy(allReceivedBytes, allReceivedBytes.Length - checksumLength, receivedCrc32, 0, checksumLength);
            Expected = expectedCrc32;
            Received = receivedCrc32;
            for (var i = 0; i < checksumLength; i++)
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
