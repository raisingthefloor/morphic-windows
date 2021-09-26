using System;
using System.IO;
using System.Security.Cryptography;

namespace IoDCLI.Cryptography
{
    public class SHA512HashProvider : IHashProvider
    {
        public string Hash(string filePath)
        {
            try
            {
                var algorithm = SHA512.Create();

                var hash = algorithm.ComputeHash(File.ReadAllBytes(filePath));

                return BitConverter.ToString(hash).Replace("-", string.Empty);
            }
            catch (IOException)
            {

            }

            return null;
        }
    }
}
