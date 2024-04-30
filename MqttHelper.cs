using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CLTLS_MQTT_GUI
{
    class MqttHelper
    {
        public static readonly int MAX_REMAINING_SIZE = 256 * 1024 * 1024 - 1;
        public static int GetMqttRemainingLengthByteCount(int remainingLength)
        {
            if (remainingLength <= 127)
            {
                return 1;
            }
            else if (remainingLength <= 127 + 127 * 128)
            {
                return 2;
            }
            else if (remainingLength <= 127 + 127 * 128 + 127 * 128 * 128)
            {
                return 3;
            }
            else
            {
                return 4;
            }
        }

        public static byte[] EncodeMqttRemainingLength(int remainingLength)
        {
            byte[] buffer = new byte[4];

            int currentIndex = 0;

            while (remainingLength > 0)
            {
                byte baseByte = remainingLength > 127 ? (byte)0x80 : (byte)0x00;
                buffer[currentIndex++] = (byte)(baseByte | (remainingLength & 0x7F));
                remainingLength >>= 7;
            }

            return new Span<byte>(buffer, 0, currentIndex).ToArray();
        }

        public static int DecodeMqttRemainingLength(Memory<byte> encoded)
        {
            var encodedSpan = encoded.Span;

            int currentByteIndex = 0;
            byte currentByte = encodedSpan[0];

            int remainingSize = 0;
            int multiplier = 1;

            while ((currentByte & 0x80) != 0)
            {
                remainingSize += multiplier * (currentByte & 0x7F);
                multiplier *= 128;
                currentByte = encodedSpan[++currentByteIndex];
            }

            return remainingSize + multiplier * (currentByte & 0x7F);
        }
    }
}
