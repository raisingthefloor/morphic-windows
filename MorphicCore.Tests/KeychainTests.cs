using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using Xunit;

namespace MorphicCore.Tests
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
            var ko = new KeychainOptions();
            ko.Path = Path.Combine(directoryName, "testsave.json");
            var uri = new Uri("http://www.morphic.world");
            var wronguri = new Uri("http://www.gpii.net");
            var user = new UsernameCredentials("passuser", "password");
            var key = new KeyCredentials("key");
            var logger = new LoggerFactory().CreateLogger<Keychain>();
            //TEST SAVING
            var kc = new Keychain(ko, encrypt, logger);
            Assert.True(kc.Save(user, uri));
            Assert.Equal(1, encrypt.encryptCounter);
            Assert.Equal(0, encrypt.decryptCounter);
            Assert.True(kc.Save(key, uri, "keyuser"));
            Assert.Equal(2, encrypt.encryptCounter);
            Assert.Equal(0, encrypt.decryptCounter);
            //TEST LOADING
            kc = new Keychain(ko, encrypt, logger);
            Assert.Equal(2, encrypt.encryptCounter);
            Assert.Equal(1, encrypt.decryptCounter);
            //TEST RETRIEVAL
            var nuser = kc.LoadUsername(uri, "passuser");
            Assert.Equal("passuser", nuser.Username);
            Assert.Equal("password", nuser.Password);
            var nkey = kc.LoadKey(uri, "keyuser");
            Assert.Equal("key", nkey.Key);
            nuser = kc.LoadUsername(uri, "notathing");
            nkey = kc.LoadKey(uri, "notathing");
            Assert.Null(nuser);
            Assert.Null(nkey);
            //TODO: put any tests for switching usernames and keys here once that does something
            nuser = kc.LoadUsername(wronguri, "passuser");
            nkey = kc.LoadKey(wronguri, "keyuser");
            Assert.Null(nuser);
            Assert.Null(nkey);
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
