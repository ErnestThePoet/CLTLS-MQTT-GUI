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
        private Socket? cltlsClientProxySocket;

        public MqttClient()
        {
            InitializeComponent();
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
        }

        private void btnCltlsClientConnect_Click(object sender, RoutedEventArgs e)
        {
            var ipEndPoint = new IPEndPoint(
                IPAddress.Parse(tbCltlsClientIp.Text),
                int.Parse(tbCltlsClientPort.Text));

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
    }
}
