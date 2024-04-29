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
        private SHA256 sha256;

        public CryptoUtils()
        {
            sha256 = SHA256.Create();
        }

        public string Sha256StringB64(string s)
        {
            var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(s));
            return Convert.ToBase64String(hash);
        }
    }
}
