using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CLTLS_MQTT_GUI
{
    class SizeHelper
    {
        private const int KB = 1024;
        private const int MB = 1024 * KB;
        public static int ParseSize(string size)
        {
            int parsedValue = 0;

            if (string.Compare(size, "MAX", true) == 0)
            {
                return MqttHelper.MAX_REMAINING_SIZE;
            }
            else if (Regex.IsMatch(size, @"^\d+$"))
            {
                return int.TryParse(size, out parsedValue) ?
                    parsedValue :
                    -1;
            }
            else if (size.EndsWith("K", true, null))
            {
                return int.TryParse(size.Substring(0, size.Length - 1), out parsedValue) ?
                    KB * parsedValue :
                    -1;
            }
            else if (size.EndsWith("KB", true, null))
            {
                return int.TryParse(size.Substring(0, size.Length - 2), out parsedValue) ?
                    KB * parsedValue :
                    -1;
            }
            else if (size.EndsWith("M", true, null))
            {
                return int.TryParse(size.Substring(0, size.Length - 1), out parsedValue) ?
                    MB * parsedValue :
                    -1;
            }
            else if (size.EndsWith("MB", true, null))
            {
                return int.TryParse(size.Substring(0, size.Length - 2), out parsedValue) ?
                    MB * parsedValue :
                    -1;
            }
            else
            {
                return -1;
            }
        }
    }
}
