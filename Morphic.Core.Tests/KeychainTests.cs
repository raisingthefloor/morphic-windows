using Microsoft.Extensions.Logging;
using System;
using System.IO;
using Xunit;

namespace Morphic.Core.Tests
{
    public class KeychainTests : IDisposable
    {
        public KeychainTests()
        {
            directoryName = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        }

        private string directoryName;
        
        [Fact]
        public void TestSaveLoad()
        {
            var encrypt = new MockEncrypter();
            var options = new KeychainOptions();
            options.Path = Path.Combine(directoryName, "testsave.json");
            var uri = new Uri("http://www.morphic.org");
            var wronguri = new Uri("http://www.gpii.net");
            var username = new UsernameCredentials("passuser", "password");
            var key = new KeyCredentials("key");
            var logger = new LoggerFactory().CreateLogger<Keychain>();
            //TEST SAVING
            var keychain = new Keychain(options, encrypt, logger);
            Assert.True(keychain.Save(username, uri));
            Assert.Equal(1, encrypt.encryptCounter);
            Assert.Equal(0, encrypt.decryptCounter);
            Assert.True(keychain.Save(key, uri, "keyuser"));
            Assert.Equal(2, encrypt.encryptCounter);
            Assert.Equal(0, encrypt.decryptCounter);
            //TEST LOADING
            keychain = new Keychain(options, encrypt, logger);
            Assert.Equal(2, encrypt.encryptCounter);
            Assert.Equal(1, encrypt.decryptCounter);
            //TEST RETRIEVAL
            var newusername = keychain.LoadUsername(uri, "passuser");
            Assert.Equal("passuser", newusername.Username);
            Assert.Equal("password", newusername.Password);
            var newkey = keychain.LoadKey(uri, "keyuser");
            Assert.Equal("key", newkey.Key);
            newusername = keychain.LoadUsername(uri, "notathing");
            newkey = keychain.LoadKey(uri, "notathing");
            Assert.Null(newusername);
            Assert.Null(newkey);
            //TODO: put any tests for switching usernames and keys here once that does something
            newusername = keychain.LoadUsername(wronguri, "passuser");
            newkey = keychain.LoadKey(wronguri, "keyuser");
            Assert.Null(newusername);
            Assert.Null(newkey);
        }

        class MockEncrypter : IDataProtection
        {
            public int encryptCounter = 0;
            public int decryptCounter = 0;
            public byte[] Protect(byte[] userData)
            {
                ++encryptCounter;
                return userData;
            }

            public byte[] Unprotect(byte[] encryptedData)
            {
                ++decryptCounter;
                return encryptedData;
            }
        }

        public void Dispose()
        {
            Directory.Delete(directoryName, true);
        }
    }
}
