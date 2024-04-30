using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;

namespace CLTLS_MQTT_GUI
{
    class CryptoUtils
    {
        private static SHA256 sha256 = SHA256.Create();

        public static string Sha256BytesB64(byte[] bytes)
        {
            return Sha256BytesB64(bytes, 0, bytes.Length);
        }

        public static string Sha256BytesB64(byte[] bytes, int offset, int count)
        {
            var hash = sha256.ComputeHash(bytes, offset, count);
            return Convert.ToBase64String(hash);
        }

        public static string Sha256StringB64(string s)
        {
            var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(s));
            return Convert.ToBase64String(hash);
        }
    }
}
