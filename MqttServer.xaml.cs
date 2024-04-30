using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace CLTLS_MQTT_GUI
{
    /// <summary>
    /// Interaction logic for MqttServer.xaml
    /// </summary>
    public partial class MqttServer : UserControl
    {
        private readonly Random random;

        private readonly CryptoUtils cryptoUtils;
        private readonly TcpHelper tcpHelper;

        public MqttServer()
        {
            random = new Random();

            cryptoUtils = new CryptoUtils();
            tcpHelper = new TcpHelper();

            InitializeComponent();

            UpdateUiServerStatus(false);
        }

        private void ShowError(string message)
        {
            MessageBox.Show(message,
                    "MQTT Server Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            return;
        }

        private void UpdateUiServerStatus(bool started)
        {
            btnServerStart.IsEnabled = !started;
            lblServerStatus.Foreground = new SolidColorBrush(
                started ? Colors.Green : Colors.Red);
            lblServerStatus.Content = started ?
                "MQTT Server Started" :
                "MQTT Server Not Started";
            gbMqttMessageReceive.Visibility = started ? Visibility.Visible : Visibility.Hidden;
            gbMqttMessageResponse.Visibility = started ? Visibility.Visible : Visibility.Hidden;

            if (started)
            {
                UpdateUiMqttPublishReceive(false);
                UpdateUiMqttPublishResponse(false);
            }
        }

        public void UpdateUiMqttPublishReceive(bool received)
        {
            lblMqttMessageReceiveStatus.Content = received ?
                "Received MQTT PUBLISH From Client" :
                "No MQTT Message Received";
            lblMqttMessageReceiveStatus.Foreground = new SolidColorBrush(received ? Colors.Green : Colors.Blue);

            if (!received)
            {
                tbMqttReceivedMessage.Clear();
                lblMqttMessageReceiveLength.Content = "-";
                lblMqttMessageReceiveSha256.Content = "-";
            }
        }

        public void UpdateUiMqttPublishResponse(bool sent)
        {
            lblMqttMessageResponseStatus.Content = sent ?
                "Sent MQTT PUBLISH To Client" :
                "No MQTT Message Response Sent";
            lblMqttMessageResponseStatus.Foreground = new SolidColorBrush(sent ? Colors.Green : Colors.Blue);

            if (!sent)
            {
                tbMqttResponseMessage.Clear();
                lblMqttMessageResponseLength.Content = "-";
                lblMqttMessageResponseSha256.Content = "-";
            }
        }

        private async void btnServerStart_Click(object sender, RoutedEventArgs e)
        {
            IPEndPoint? ipEndPoint = null;

            try
            {
                ipEndPoint = new IPEndPoint(
                IPAddress.Parse("0.0.0.0"),
                int.Parse(tbServerPort.Text));
            }
            catch
            {
                ShowError("IP address or port number invalid");
                return;
            }

            using Socket listenSocket = new(ipEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            try
            {
                listenSocket.Bind(ipEndPoint);
            }
            catch
            {
                ShowError("Bind() failed");
                return;
            }

            listenSocket.Listen();

            UpdateUiServerStatus(true);

            while (true)
            {
                using Socket cltlsServerProxySocket = await listenSocket.AcceptAsync();

                while (true)
                {
                    byte[]? receiveFixedHeader = null;

                    try
                    {
                        receiveFixedHeader = await tcpHelper.TcpRecvAsync(cltlsServerProxySocket, 2);
                    }
                    catch
                    {
                        ShowError("Failed to receive MQTT fixed header");
                        break;
                    }

                    if ((receiveFixedHeader[0] >>> 4) != MqttHelper.MSG_TYPE_PUBLISH)
                    {
                        ShowError(
                            $"Received MQTT message type is not PUBLISH (first byte is {receiveFixedHeader[0]:X2})");
                        break;
                    }

                    byte receiveCurrentByte = receiveFixedHeader[1];
                    int receivePayloadSize = 0;
                    int receiveMultiplier = 1;

                    bool breakLoop = false;

                    while ((receiveCurrentByte & 0x80) != 0)
                    {
                        receivePayloadSize += receiveMultiplier * (receiveCurrentByte & 0x7F);
                        receiveMultiplier *= 128;

                        try
                        {
                            receiveCurrentByte = (await tcpHelper.TcpRecvAsync(cltlsServerProxySocket!, 1))[0];
                        }
                        catch
                        {
                            ShowError("Failed to receive MQTT payload size");
                            breakLoop = true;
                            break;
                        }
                    }

                    if (breakLoop)
                    {
                        break;
                    }

                    receivePayloadSize += receiveMultiplier * (receiveCurrentByte & 0x7F);

                    byte[]? receivePayload = null;

                    try
                    {
                        receivePayload = await tcpHelper.TcpRecvAsync(cltlsServerProxySocket!, receivePayloadSize);
                    }
                    catch
                    {
                        ShowError("Failed to receive MQTT payload");
                        break;
                    }

                    if ((receiveFixedHeader[0] >>> 4) == MqttHelper.MSG_TYPE_DISCONNECT)
                    {
                        break;
                    }

                    UpdateUiMqttPublishReceive(true);
                    lblMqttMessageReceiveLength.Content = receivePayloadSize.ToString();
                    lblMqttMessageReceiveSha256.Content = CryptoUtils.Sha256BytesB64(receivePayload);

                    byte[]? responsePayload = null;

                    if (receivePayload[0] == Constants.PAYLOAD_TYPE_UTF8)
                    {
                        string receivedText = Encoding.UTF8.GetString(
                            new Memory<byte>(receivePayload).Slice(1).Span);

                        tbMqttReceivedMessage.Text = receivedText;

                        string responseText =
                            $"It's so nice to receive \"{receivedText}\" from client via the CL-TLS protocol.";

                        tbMqttResponseMessage.Text = responseText;

                        var responseTextBytes = Encoding.UTF8.GetBytes(responseText);

                        responsePayload = new byte[responseTextBytes.Length + 1];
                        responsePayload[0] = Constants.PAYLOAD_TYPE_UTF8;
                        responseTextBytes.CopyTo(responsePayload, 1);
                    }
                    else
                    {
                        tbMqttReceivedMessage.Text = "(Binary Data)";
                        tbMqttResponseMessage.Text = "(Binary Data)";

                        responsePayload = receivePayload;
                        random.NextBytes(responsePayload);
                        responsePayload[0] = Constants.PAYLOAD_TYPE_BINARY;
                    }

                    lblMqttMessageResponseLength.Content = responsePayload.Length.ToString();
                    lblMqttMessageResponseSha256.Content = CryptoUtils.Sha256BytesB64(responsePayload);

                    var encodedResponseSize = MqttHelper.EncodeMqttRemainingLength(responsePayload.Length);

                    byte[] responseBuffer = new byte[1 + encodedResponseSize.Length + responsePayload.Length];

                    responseBuffer[0] = MqttHelper.PUBLISH_FIRST_BYTE;
                    encodedResponseSize.CopyTo(responseBuffer, 1);
                    responsePayload.CopyTo(responseBuffer, 1 + encodedResponseSize.Length);

                    try
                    {
                        await tcpHelper.TcpSendAsync(
                            cltlsServerProxySocket!,
                            responseBuffer);
                    }
                    catch
                    {
                        ShowError("Failed to send MQTT PUBLISH");
                        break;
                    }

                    UpdateUiMqttPublishResponse(true);
                }
            }
        }
    }
}
