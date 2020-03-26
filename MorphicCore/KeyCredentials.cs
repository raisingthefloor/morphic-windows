using System;
using System.Security.Cryptography;

namespace MorphicCore
{

    /// <summary>
    /// Secret key based credentials
    /// </summary>
    public class KeyCredentials : ICredentials
    {

        public KeyCredentials()
        {
            var provider = RandomNumberGenerator.Create();
            var data = new byte[64];
            provider.GetBytes(data);
            Key = Convert.ToBase64String(data);
        }

        public KeyCredentials(string key)
        {
            Key = key;
        }

        public string Key { get; set; }
    }
}
