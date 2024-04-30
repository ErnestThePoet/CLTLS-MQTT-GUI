using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace CLTLS_MQTT_GUI
{
    class TcpHelper
    {
        public async Task TcpSendAsync(Socket socket, byte[] data)
        {
            Memory<byte> dataMemory = data;
            await TcpSendAsync(socket, dataMemory);
        }

        public async Task TcpSendAsync(Socket socket, Memory<byte> data)
        {
            int sent_size = 0;

            while (sent_size < data.Length)
            {
                int current_send_size = await socket.SendAsync(data.Slice(sent_size));

                sent_size += current_send_size;
            }
        }

        public async Task<byte[]> TcpRecvAsync(Socket socket, int length)
        {
            byte[] buffer = new byte[length];
            ArraySegment<byte> bufferSegment = buffer;

            int received_size = 0;

            while (received_size < length)
            {
                int current_receive_size = await socket.ReceiveAsync(bufferSegment.Slice(received_size));

                received_size += current_receive_size;
            }

            return buffer;
        }
    }
}
