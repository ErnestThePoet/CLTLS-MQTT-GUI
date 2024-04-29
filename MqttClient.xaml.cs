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

        private readonly CryptoUtils cryptoUtils;
        private readonly TcpHelper tcpHelper;

        private Socket? cltlsClientProxySocket;
        private MqttPublishMessageSource mqttPublishMessageSource;
        private string mqttPublishPayload;

        public MqttClient()
        {
            cryptoUtils = new CryptoUtils();
            tcpHelper = new TcpHelper();

            mqttPublishMessageSource = MqttPublishMessageSource.ENTER_TEXT;
            mqttPublishPayload = string.Empty;

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
        }

        private void SyncMqttPublishMessageLengthSha256()
        {
            lblMqttMessagePublishLength.Content = mqttPublishPayload.Length;
            lblMqttMessagePublishSha256.Content = cryptoUtils.Sha256StringB64(mqttPublishPayload);
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
                UpdateUiCltlsClentProxyConnection(false);
            }
        }

        private void btnCltlsClientDisconnect_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                cltlsClientProxySocket!.Disconnect(false);
                UpdateUiCltlsClentProxyConnection(false);
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

            Memory<byte> connectRequestMemory = connectRequest;

            try
            {
                Convert.FromHexString(tbServerId.Text).CopyTo(connectRequestMemory.Slice(1, 8));
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
                mqttPublishPayload = tbMqttMessage.Text;
                SyncMqttPublishMessageLengthSha256();
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
    }
}
