using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace CLTLS_MQTT_GUI
{
    class Error
    {
        public static void ShowError(string message)
        {
            MessageBox.Show(message,
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            return;
        }
    }
}
