using Microsoft.Extensions.Logging;
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
            ko.Path = GetTestFile("testusersave.json");
            var uri = new Uri("http://www.morphic.world");
            var user = new UsernameCredentials("supersecret", "swordfish");
            var key = new KeyCredentials("thekey");
            var loggerFactory = new LoggerFactory();
            var logger = loggerFactory.CreateLogger<Keychain>();
            var kc = new Keychain(ko, new MockEncrypter(), logger);
            Assert.True(kc.Save(user, uri));
            var a = File.ReadAllBytes(GetTestFile("testusersave.json"));
            var b = File.ReadAllBytes(GetTestFile("saveuserref.json"));
            Assert.Equal(b, a);
            ko = new KeychainOptions();
            ko.Path = GetTestFile("testkeysave.json");
            kc = new Keychain(ko, new MockEncrypter(), logger);
            Assert.True(kc.Save(key, uri, "theusername"));
            a = File.ReadAllBytes(GetTestFile("testkeysave.json"));
            b = File.ReadAllBytes(GetTestFile("savekeyref.json"));
            Assert.Equal(b, a);
            ko = new KeychainOptions();
            ko.Path = GetTestFile("testcombsave.json");
            kc = new Keychain(ko, new MockEncrypter(), logger);
            Assert.True(kc.Save(user, uri));
            Assert.True(kc.Save(key, uri, "theusername"));
            a = File.ReadAllBytes(GetTestFile("testcombsave.json"));
            b = File.ReadAllBytes(GetTestFile("savecombref.json"));
            Assert.Equal(b, a);
            //TODO: so this actually can just write any file type. Verify this is expected behavior.
            ko.Path = GetTestFile("notajsonfile");
            kc = new Keychain(ko, new MockEncrypter(), logger);
            kc.Save(user, uri);
            kc.Save(key, uri, "theusername");
            a = File.ReadAllBytes(GetTestFile("notajsonfile"));
            b = File.ReadAllBytes(GetTestFile("savecombref.json"));
            Assert.Equal(b, a);
        }

        [Fact]
        public void TestLoadKey()
        {
            var ko = new KeychainOptions();
            ko.Path = GetTestFile("biguserlist.json");
            var loggerFactory = new LoggerFactory();
            var logger = loggerFactory.CreateLogger<Keychain>();
            var uri = new Uri("http://www.morphic.world");
            var wronguri = new Uri("http://www.gpii.net");
            Keychain kc = new Keychain(ko, new MockEncrypter(), logger);
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
            ko.Path = GetTestFile("biguserlist.json");
            var loggerFactory = new LoggerFactory();
            var logger = loggerFactory.CreateLogger<Keychain>();
            var uri = new Uri("http://www.morphic.world");
            var wronguri = new Uri("http://www.gpii.net");
            Keychain kc = new Keychain(ko, new MockEncrypter(), logger);
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

        private string GetTestFile(string file)
        {
            return Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..\\..\\..\\testfiles\\keychain", file));
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
            File.Delete(GetTestFile("testkeysave.json"));
            File.Delete(GetTestFile("testusersave.json"));
            File.Delete(GetTestFile("testcombsave.json"));
            File.Delete(GetTestFile("notajsonfile"));
        }
    }
}
