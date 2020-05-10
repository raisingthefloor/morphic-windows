using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Xunit;

namespace MorphicCore.Tests
{
    public class KeychainTests : IDisposable
    {

        [Fact]
        public void TestSave()
        {
            var ko = new KeychainOptions();
            ko.Path = "../../../testfiles/keychain/testsave.json";
            var uri = new Uri("http://www.morphic.world");
            var user = new UsernameCredentials("supersecret", "swordfish");
            var key = new KeyCredentials("thekey");
            var kc = new Keychain(ko, new MockEncrypter(), new NullLogger<Keychain>());
            Assert.True(kc.Save(user, uri));
            var a = File.ReadAllBytes("../../../testfiles/keychain/testsave.json");
            var b = File.ReadAllBytes("../../../testfiles/keychain/saveuserref.json");
            Assert.Equal(b, a);
            File.Delete("../../../testfiles/keychain/testsave.json");
            kc = new Keychain(ko, new MockEncrypter(), new NullLogger<Keychain>());
            Assert.True(kc.Save(key, uri, "theusername"));
            a = File.ReadAllBytes("../../../testfiles/keychain/testsave.json");
            b = File.ReadAllBytes("../../../testfiles/keychain/savekeyref.json");
            Assert.Equal(b, a);
            File.Delete("../../../testfiles/keychain/testsave.json");
            kc = new Keychain(ko, new MockEncrypter(), new NullLogger<Keychain>());
            Assert.True(kc.Save(user, uri));
            Assert.True(kc.Save(key, uri, "theusername"));
            a = File.ReadAllBytes("../../../testfiles/keychain/testsave.json");
            b = File.ReadAllBytes("../../../testfiles/keychain/savecombref.json");
            Assert.Equal(b, a);
            File.Delete("../../../testfiles/keychain/testsave.json");
            //TODO: so this actually can just write any file type. Verify this is expected behavior.
            ko.Path = "../../../testfiles/keychain/notajsonfile";
            kc = new Keychain(ko, new MockEncrypter(), new NullLogger<Keychain>());
            kc.Save(user, uri);
            kc.Save(key, uri, "theusername");
            a = File.ReadAllBytes("../../../testfiles/keychain/notajsonfile");
            b = File.ReadAllBytes("../../../testfiles/keychain/savecombref.json");
            Assert.Equal(b, a);
            File.Delete("../../../testfiles/keychain/notajsonfile");
        }

        [Fact]
        public void TestLoadKey()
        {
            var ko = new KeychainOptions();
            ko.Path = "../../../testfiles/keychain/biguserlist.json";
            var uri = new Uri("http://www.morphic.world");
            var wronguri = new Uri("http://www.gpii.net");
            Keychain kc = new Keychain(ko, new MockEncrypter(), new NullLogger<Keychain>());
            var bob = kc.LoadKey(uri, "bob");   //bob is a key account
            Assert.NotNull(bob);
            Assert.Equal("bobkey", bob.Key);
            var shirley = kc.LoadKey(uri, "shirley");   //no account at all
            Assert.Null(shirley);
            var steve = kc.LoadKey(uri, "steve");   //steve is a password account
            //Assert.Null(steve);   //TODO: should we be differentiating between passwords and keys? And allowing multiple entry strats so you can have a key and a password?
            var bobagain = kc.LoadKey(wronguri, "bob"); //bob doesn't have a gpii.net endpoint
            Assert.Null(bobagain);
        }

        [Fact]
        public void TestLoadUsername()
        {
            var ko = new KeychainOptions();
            ko.Path = "../../../testfiles/keychain/biguserlist.json";
            var uri = new Uri("http://www.morphic.world");
            var wronguri = new Uri("http://www.gpii.net");
            Keychain kc = new Keychain(ko, new MockEncrypter(), new NullLogger<Keychain>());
            var dave = kc.LoadUsername(uri, "dave");   //dave is a password account
            Assert.NotNull(dave);
            Assert.Equal("dave", dave.Username);
            Assert.Equal("davepassword", dave.Password);
            var shirley = kc.LoadUsername(uri, "shirley");   //no account at all
            Assert.Null(shirley);
            var alex = kc.LoadUsername(uri, "alex");   //alex is a key account
            //Assert.Null(alex);
            var daveagain = kc.LoadUsername(wronguri, "dave"); //dave doesn't have a gpii.net endpoint
            Assert.Null(daveagain);
        }

        class MockEncrypter : IDataProtection
        {
            public byte[] Protect(byte[] userData)
            {
                byte[] reply = new byte[userData.Length];
                for(int i = 0; i < userData.Length; ++i)
                {
                    reply[reply.Length - i - 1] = userData[i];
                }
                return reply;
            }

            public byte[] Unprotect(byte[] encryptedData)
            {
                return Protect(encryptedData);
            }
        }

        public void Dispose()
        {
            File.Delete("../../../testfiles/keychain/testsave.json");
            File.Delete("../../../testfiles/keychain/notajsonfile");
        }
    }
}
