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
    /// Interaction logic for MqttClient.xaml
    /// </summary>
    public partial class MqttClient : UserControl
    {
        enum MqttPublishMessageSource
        {
            ENTER_TEXT,
            ENTER_SIZE
        }

        private readonly Random random;
        private readonly CryptoUtils cryptoUtils;
        private readonly TcpHelper tcpHelper;

        private Socket? cltlsClientProxySocket;
        private MqttPublishMessageSource mqttPublishMessageSource;

        public MqttClient()
        {
            random = new Random();

            cryptoUtils = new CryptoUtils();
            tcpHelper = new TcpHelper();

            mqttPublishMessageSource = MqttPublishMessageSource.ENTER_TEXT;

            InitializeComponent();

            UpdateUiCltlsClentProxyConnection(false);
            UpdateUiServerConnection(false);
        }

        private void ShowError(string message)
        {
            MessageBox.Show(message,
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            return;
        }

        private void UpdateUiCltlsClentProxyConnection(bool connected)
        {
            btnCltlsClientConnect.IsEnabled = !connected;
            btnCltlsClientDisconnect.IsEnabled = connected;
            lblCltlsClientConnectionStatus.Foreground = new SolidColorBrush(
                connected ? Colors.Green : Colors.Red);
            lblCltlsClientConnectionStatus.Content = connected ?
                "CL-TLS Client Proxy Connected" :
                "CL-TLS Client Proxy Not Connected";
            gbServerConnection.Visibility = connected ? Visibility.Visible : Visibility.Hidden;

            if (!connected)
            {
                UpdateUiServerConnection(false);
            }
        }

        private void UpdateUiServerConnection(bool connected)
        {
            lblServerConnectionStatus.Foreground = new SolidColorBrush(
                connected ? Colors.Green : Colors.Red);
            lblServerConnectionStatus.Content = connected ?
                "Server Connected" :
                "Server Not Connected";
            gbMqttMessagePublish.Visibility = connected ? Visibility.Visible : Visibility.Hidden;
            gbMqttMessageRecv.Visibility = connected ? Visibility.Visible : Visibility.Hidden;
            btnServerConnect.IsEnabled = !connected;
            btnServerDisconnect.IsEnabled = connected;

            if (connected)
            {
                rbEnterText.IsChecked = true;
                tbMqttMessage.Text = "Hello World";
                UpdateUiMqttPublishDelivery(false);
                UpdateUiMqttPublishReceive(false);
            }
        }

        public void UpdateUiMqttPublishDelivery(bool delivered)
        {
            lblMqttDeliveryStatus.Content = delivered ? "Delivered" : "Not Delivered";
            lblMqttDeliveryStatus.Foreground = new SolidColorBrush(delivered ? Colors.Green : Colors.Blue);
        }

        public void UpdateUiMqttPublishReceive(bool received)
        {
            lblMqttMessageReceiveStatus.Content = received ?
                "Received MQTT PUBLISH From Server" :
                "No MQTT Message Received";
            lblMqttMessageReceiveStatus.Foreground = new SolidColorBrush(received ? Colors.Green : Colors.Blue);

            if (!received)
            {
                tbMqttReceivedMessage.Clear();
                lblMqttMessageReceiveLength.Content = "-";
                lblMqttMessageReceiveSha256.Content = "-";
            }
        }

        private void btnCltlsClientConnect_Click(object sender, RoutedEventArgs e)
        {
            IPEndPoint? ipEndPoint = null;

            try
            {
                ipEndPoint = new IPEndPoint(
                IPAddress.Parse(tbCltlsClientIp.Text),
                int.Parse(tbCltlsClientPort.Text));
            }
            catch
            {
                ShowError("IP address or port number invalid");
                return;
            }

            cltlsClientProxySocket = new(
                ipEndPoint.AddressFamily,
                SocketType.Stream,
                ProtocolType.Tcp);

            try
            {
                cltlsClientProxySocket.Connect(ipEndPoint);
                UpdateUiCltlsClentProxyConnection(true);
            }
            catch
            {
                ShowError("Failed to connect to CL-TLS Client Proxy");
                return;
            }
        }

        private void btnCltlsClientDisconnect_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                cltlsClientProxySocket!.Disconnect(false);
                UpdateUiCltlsClentProxyConnection(false);
                UpdateUiMqttPublishDelivery(false);
                UpdateUiMqttPublishReceive(false);
            }
            catch
            { }
        }

        private async void btnServerConnect_Click(object sender, RoutedEventArgs e)
        {
            if (cltlsClientProxySocket == null)
            {
                return;
            }

            if (tbServerId.Text.Length != 16)
            {
                ShowError("Server identity must be 8-bytes (16 hex chars)");
                return;
            }

            int serverPort = 0;
            if (!int.TryParse(tbServerPort.Text, out serverPort) || serverPort < 0 || serverPort > 65535)
            {
                ShowError("Server port invalid");
                return;
            }

            byte[] connectRequest = new byte[11];

            connectRequest[0] = 0x00; // CONNCTL_MSG_TYPE_CONNECT_REQUEST

            unsafe
            {
                short serverPortNetworkOrder = IPAddress.HostToNetworkOrder((short)serverPort);

                fixed (byte* pConnectRequest = connectRequest)
                {
                    *(short*)(pConnectRequest + 9) = serverPortNetworkOrder;
                }
            }

            try
            {
                Convert.FromHexString(tbServerId.Text).CopyTo(
                    new Memory<byte>(connectRequest).Slice(1, 8));
            }
            catch
            {
                ShowError("Server identity is not a valid hex string");
                return;
            }

            try
            {
                await tcpHelper.TcpSendAsync(cltlsClientProxySocket, connectRequest);
            }
            catch
            {
                ShowError("Failed to send CONNCTL Connect Request");
                return;
            }

            byte[]? connectResponse = null;

            try
            {
                connectResponse = await tcpHelper.TcpRecvAsync(cltlsClientProxySocket, 2);
            }
            catch
            {
                ShowError("Failed to receive CONNCTL Connect Response");
                return;
            }

            if (connectResponse[0] != 0x10)
            {
                ShowError($"Invalid response message type (0x{connectResponse[0]:X2}) received; " +
                         "expecting CONNCTL_MSG_TYPE_CONNECT_RESPONSE (0x10)");
                return;
            }

            if (connectResponse[1] == 0xF0)
            {
                ShowError("CL-TLS Client reports connection failed");
                return;
            }

            UpdateUiServerConnection(true);
        }

        private async void btnServerDisconnect_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await tcpHelper.TcpSendAsync(cltlsClientProxySocket!, [0xe0, 0x00]);
                UpdateUiServerConnection(false);
            }
            catch
            {
                ShowError("Failed to send MQTT DISCONNECT");
                return;
            }
        }

        private void tbMqttMessage_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (mqttPublishMessageSource == MqttPublishMessageSource.ENTER_TEXT)
            {
                lblMqttMessagePublishLength.Content = tbMqttMessage.Text.Length.ToString();
                lblMqttMessagePublishSha256.Content = CryptoUtils.Sha256StringB64(tbMqttMessage.Text);
            }
        }

        private void rbEnterText_Checked(object sender, RoutedEventArgs e)
        {
            mqttPublishMessageSource = MqttPublishMessageSource.ENTER_TEXT;
        }

        private void rbEnterSize_Checked(object sender, RoutedEventArgs e)
        {
            mqttPublishMessageSource = MqttPublishMessageSource.ENTER_SIZE;
            lblMqttMessagePublishLength.Content = "-";
            lblMqttMessagePublishSha256.Content = "-";
        }

        private async void btnMqttPublish_Click(object sender, RoutedEventArgs e)
        {
            int sendPayloadSize = 0;
            byte[]? sendPayload = null;

            if (mqttPublishMessageSource == MqttPublishMessageSource.ENTER_SIZE)
            {
                sendPayloadSize = SizeHelper.ParseSize(tbMqttMessage.Text);
                if (sendPayloadSize < 0)
                {
                    ShowError("Invalid size");
                    return;
                }

                if (sendPayloadSize > MqttHelper.MAX_REMAINING_SIZE)
                {
                    ShowError("Size must be <= 256MB-1");
                    return;
                }

                // With additional one byte indicating whether payload is text
                sendPayloadSize += 1;

                sendPayload = new byte[sendPayloadSize];

                random.NextBytes(sendPayload);

                sendPayload[0] = Constants.PAYLOAD_TYPE_BINARY;

                lblMqttMessagePublishLength.Content = sendPayloadSize.ToString();
                lblMqttMessagePublishSha256.Content = CryptoUtils.Sha256BytesB64(sendPayload);
            }
            else
            {
                var textBytes = Encoding.UTF8.GetBytes(tbMqttMessage.Text);

                sendPayloadSize = textBytes.Length + 1;

                sendPayload = new byte[sendPayloadSize];
                sendPayload[0] = Constants.PAYLOAD_TYPE_UTF8;
                textBytes.CopyTo(sendPayload, 1);
            }

            var encodedSendSize = MqttHelper.EncodeMqttRemainingLength(sendPayloadSize);

            byte[] sendBuffer = new byte[1 + encodedSendSize.Length + sendPayloadSize];

            sendBuffer[0] = 0x30;
            encodedSendSize.CopyTo(sendBuffer, 1);
            sendPayload.CopyTo(sendBuffer, 1 + encodedSendSize.Length);

            try
            {
                await tcpHelper.TcpSendAsync(
                    cltlsClientProxySocket!,
                    sendBuffer);
            }
            catch
            {
                ShowError("Failed to send MQTT PUBLISH");
                return;
            }

            UpdateUiMqttPublishDelivery(true);

            byte[]? receiveFixedHeader = null;

            try
            {
                receiveFixedHeader = await tcpHelper.TcpRecvAsync(cltlsClientProxySocket!, 2);
            }
            catch
            {
                ShowError("Failed to receive MQTT fixed header");
                return;
            }

            byte receiveCurrentByte = receiveFixedHeader[1];
            int receivePayloadSize = 0;
            int receiveMultipler = 1;

            while ((receiveCurrentByte & 0x80) != 0)
            {
                receivePayloadSize += receiveMultipler * (receiveCurrentByte & 0x7F);
                receiveMultipler *= 128;

                try
                {
                    receiveCurrentByte = (await tcpHelper.TcpRecvAsync(cltlsClientProxySocket!, 1))[0];
                }
                catch
                {
                    ShowError("Failed to receive MQTT payload");
                    return;
                }
            }

            receivePayloadSize += receiveMultipler * (receiveCurrentByte & 0x7F);

            byte[]? receivePayload = null;

            try
            {
                receivePayload = await tcpHelper.TcpRecvAsync(cltlsClientProxySocket!, receivePayloadSize);
            }
            catch
            {
                ShowError("Failed to receive MQTT fixed header");
                return;
            }

            if (receivePayload[0] == Constants.PAYLOAD_TYPE_UTF8)
            {
                tbMqttReceivedMessage.Text = Encoding.UTF8.GetString(
                    new Memory<byte>(receivePayload).Slice(1).Span);
            }
            else
            {
                tbMqttReceivedMessage.Text = "(Binary Data)";
            }

            UpdateUiMqttPublishReceive(true);
            lblMqttMessageReceiveLength.Content = receivePayloadSize.ToString();
            lblMqttMessageReceiveSha256.Content = CryptoUtils.Sha256BytesB64(receivePayload);
        }
    }
}
