// Copyright 2020 Raising the Floor - International
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/GPII/universal/blob/master/LICENSE.txt
//
// The R&D leading to these results received funding from the:
// * Rehabilitation Services Administration, US Dept. of Education under 
//   grant H421A150006 (APCP)
// * National Institute on Disability, Independent Living, and 
//   Rehabilitation Research (NIDILRR)
// * Administration for Independent Living & Dept. of Education under grants 
//   H133E080022 (RERC-IT) and H133E130028/90RE5003-01-00 (UIITA-RERC)
// * European Union's Seventh Framework Programme (FP7/2007-2013) grant 
//   agreement nos. 289016 (Cloud4all) and 610510 (Prosperity4All)
// * William and Flora Hewlett Foundation
// * Ontario Ministry of Research and Innovation
// * Canadian Foundation for Innovation
// * Adobe Foundation
// * Consumer Electronics Association Foundation

using Microsoft.Extensions.Logging;
using Morphic.Settings.Ini;
using NuGet.Frameworks;
using System;
using System.Security;
using System.Threading.Tasks;
using Xunit;


namespace Morphic.Settings.Tests
{

    public class IniSettingHandlerTests
    {
        private static int openCount = 0;
        private static int getCount = 0;
        private static int setCount = 0;

#nullable enable
        private class MockIniFile : IIniFile
        {
            public delegate string? GetResponder(string section, string key);
            public delegate void SetResponder(string section, string key, string value);

            public static GetResponder nextGetResponder = (string section, string key) =>
            {
                throw new NotImplementedException();
            };

            public static SetResponder nextSetResponder = (string section, string key, string value) =>
            {
                throw new NotImplementedException();
            };

            public string? GetValue(string section, string key)
            {
                ++getCount;
                return nextGetResponder(section, key);
            }

            public void SetValue(string section, string key, string value)
            {
                ++setCount;
                nextSetResponder(section, key, value);
            }
        }
#nullable disable

        private class MockIniFactory : IIniFileFactory
        {
            public IIniFile Open(string path)
            {
                ++openCount;
                return new MockIniFile();
            }
        }

        [Theory]
        [InlineData(Setting.ValueKind.String, "Hello", "Hello")]
        [InlineData(Setting.ValueKind.Integer, "52", 52L)]
        [InlineData(Setting.ValueKind.Double, "3.14159", 3.14159d)]
        [InlineData(Setting.ValueKind.Double, "12345", 12345.0d)]
        [InlineData(Setting.ValueKind.Boolean, "1", true)]
        [InlineData(Setting.ValueKind.Boolean, "0", false)]
        public async Task TestCapturePass(Setting.ValueKind kind, string storedvalue, object value)
        {
            var mockFactory = new MockIniFactory();
            var loggerFactory = new LoggerFactory();
            var logger = loggerFactory.CreateLogger<IniSettingHandler>();
            openCount = 0;
            getCount = 0;
            setCount = 0;

            var setting = new Setting()
            {
                Name = "test",
                Kind = kind,
                HandlerDescription = new IniSettingHandlerDescription("thefile", "thesection", "thekey")
            };
            var handler = new IniSettingHandler(setting, mockFactory, logger);

            MockIniFile.nextGetResponder = (string section, string key) =>
            {
                Assert.Equal("thesection", section);
                Assert.Equal("thekey", key);
                return storedvalue;
            };
            var result = await handler.Capture();
            Assert.Equal(1, openCount);
            Assert.Equal(1, getCount);
            Assert.Equal(0, setCount);
            Assert.True(result.Success);
            Assert.NotNull(result.Value);
            Assert.Equal(value, result.Value);
        }

#nullable enable
        [Theory]
        [InlineData(Setting.ValueKind.Integer, "ayy lmao")]
        [InlineData(Setting.ValueKind.Integer, "3.14159")]
        [InlineData(Setting.ValueKind.Double, "ayy lmao")]
        [InlineData(Setting.ValueKind.String, null)]
        [InlineData(Setting.ValueKind.Integer, null)]
        [InlineData(Setting.ValueKind.Double, null)]
        [InlineData(Setting.ValueKind.Boolean, null)]
        public async Task TestCaptureFail(Setting.ValueKind kind, string? storedvalue)
        {
            var mockFactory = new MockIniFactory();
            var loggerFactory = new LoggerFactory();
            var logger = loggerFactory.CreateLogger<IniSettingHandler>();
            openCount = 0;
            getCount = 0;
            setCount = 0;

            var setting = new Setting()
            {
                Name = "test",
                Kind = kind,
                HandlerDescription = new IniSettingHandlerDescription("thefile", "thesection", "thekey")
            };
            var handler = new IniSettingHandler(setting, mockFactory, logger);

            MockIniFile.nextGetResponder = (string section, string key) =>
            {
                Assert.Equal("thesection", section);
                Assert.Equal("thekey", key);
                return storedvalue;
            };
            var result = await handler.Capture();
            Assert.Equal(1, openCount);
            Assert.Equal(1, getCount);
            Assert.Equal(0, setCount);
            Assert.False(result.Success);
            Assert.Null(result.Value);
        }
#nullable disable

        [Fact]
        public async Task TestCaptureException()
        {
            var mockFactory = new MockIniFactory();
            var loggerFactory = new LoggerFactory();
            var logger = loggerFactory.CreateLogger<IniSettingHandler>();

            var setting = new Setting()
            {
                Name = "test",
                Kind = Setting.ValueKind.String,
                HandlerDescription = new IniSettingHandlerDescription("thefile", "thesection", "thekey")
            };

            openCount = 0;
            getCount = 0;
            setCount = 0;
            var handler = new IniSettingHandler(setting, mockFactory, logger);
            MockIniFile.nextGetResponder = (string section, string key) =>
            {
                throw new ArgumentException();
            };
            var result = await handler.Capture();
            Assert.Equal(1, openCount);
            Assert.Equal(1, getCount);
            Assert.Equal(0, setCount);
            Assert.False(result.Success);
            Assert.Null(result.Value);
            openCount = 0;
            getCount = 0;
            setCount = 0;
            handler = new IniSettingHandler(setting, mockFactory, logger);
            MockIniFile.nextGetResponder = (string section, string key) =>
            {
                throw new ArgumentNullException();
            };
            result = await handler.Capture();
            Assert.Equal(1, openCount);
            Assert.Equal(1, getCount);
            Assert.Equal(0, setCount);
            Assert.False(result.Success);
            Assert.Null(result.Value);
            openCount = 0;
            getCount = 0;
            setCount = 0;
            handler = new IniSettingHandler(setting, mockFactory, logger);
            MockIniFile.nextGetResponder = (string section, string key) =>
            {
                throw new SecurityException();
            };
            result = await handler.Capture();
            Assert.Equal(1, openCount);
            Assert.Equal(1, getCount);
            Assert.Equal(0, setCount);
            Assert.False(result.Success);
            Assert.Null(result.Value);
            openCount = 0;
            getCount = 0;
            setCount = 0;
            handler = new IniSettingHandler(setting, mockFactory, logger);
            MockIniFile.nextGetResponder = (string section, string key) =>
            { 
                throw new ObjectDisposedException("ayylmao");
            };
            result = await handler.Capture();
            Assert.Equal(1, openCount);
            Assert.Equal(1, getCount);
            Assert.Equal(0, setCount);
            Assert.False(result.Success);
            Assert.Null(result.Value);
            openCount = 0;
            getCount = 0;
            setCount = 0;
            handler = new IniSettingHandler(setting, mockFactory, logger);
            MockIniFile.nextGetResponder = (string section, string key) =>
            { 
                throw new System.IO.IOException(); 
            };
            result = await handler.Capture();
            Assert.Equal(1, openCount);
            Assert.Equal(1, getCount);
            Assert.Equal(0, setCount);
            Assert.False(result.Success);
            Assert.Null(result.Value);
            openCount = 0;
            getCount = 0;
            setCount = 0;
            handler = new IniSettingHandler(setting, mockFactory, logger);
            MockIniFile.nextGetResponder = (string section, string key) =>
            {
                throw new UnauthorizedAccessException();
            };
            result = await handler.Capture();
            Assert.Equal(1, openCount);
            Assert.Equal(1, getCount);
            Assert.Equal(0, setCount);
            Assert.False(result.Success);
            Assert.Null(result.Value);
        }

        [Theory]
        [InlineData(Setting.ValueKind.String, "Hello", "Hello")]
        [InlineData(Setting.ValueKind.Integer, 52L, "52")]
        [InlineData(Setting.ValueKind.Boolean, true, "1")]
        [InlineData(Setting.ValueKind.Double, 3.14159d, "3.14159")]
        public async Task TestApplyPass(Setting.ValueKind kind, object value, string filevalue)
        {
            var mockFactory = new MockIniFactory();
            var loggerFactory = new LoggerFactory();
            var logger = loggerFactory.CreateLogger<IniSettingHandler>();
            openCount = 0;
            getCount = 0;
            setCount = 0;

            var setting = new Setting()
            {
                Name = "test",
                Kind = kind,
                HandlerDescription = new IniSettingHandlerDescription("thefile", "thesection", "thekey")
            };
            var handler = new IniSettingHandler(setting, mockFactory, logger);

            MockIniFile.nextSetResponder = (string section, string key, string value) =>
            {
                Assert.Equal("thesection", section);
                Assert.Equal("thekey", key);
                Assert.Equal(filevalue, value);
            };
            var pass = await handler.Apply(value);
            Assert.Equal(1, openCount);
            Assert.Equal(0, getCount);
            Assert.Equal(1, setCount);
            Assert.True(pass);
        }

        /* leaving here in case of failure case that isn't an exception
        [Theory]
        public async Task TestApplyFail(Setting.ValueKind kind, object? value)
        {
            var mockFactory = new MockIniFactory();
            var loggerFactory = new LoggerFactory();
            var logger = loggerFactory.CreateLogger<IniSettingHandler>();
            openCount = 0;
            getCount = 0;
            setCount = 0;

            var setting = new Setting()
            {
                Name = "test",
                Kind = kind,
                HandlerDescription = new IniSettingHandlerDescription("thefile", "thesection", "thekey")
            };
            var handler = new IniSettingHandler(setting, mockFactory, logger);
            MockIniFile.nextSetResponder = (string section, string key, string value) =>
            {
                Assert.Equal("thesection", section);
                Assert.Equal("thekey", key);
            };
            var pass = await handler.Apply(value);
            Assert.Equal(1, openCount);
            Assert.Equal(0, getCount);
            Assert.Equal(1, setCount);
            Assert.False(pass);
        }
        */

        [Fact]
        public async Task TestApplyException()
        {
            var mockFactory = new MockIniFactory();
            var loggerFactory = new LoggerFactory();
            var logger = loggerFactory.CreateLogger<IniSettingHandler>();

            var setting = new Setting()
            {
                Name = "test",
                Kind = Setting.ValueKind.String,
                HandlerDescription = new IniSettingHandlerDescription("thefile", "thesection", "thekey")
            };

            openCount = 0;
            getCount = 0;
            setCount = 0;
            var handler = new IniSettingHandler(setting, mockFactory, logger);
            MockIniFile.nextSetResponder = (string section, string key, string value) =>
            {
                throw new ArgumentException(); 
            };
            var pass = await handler.Apply("ayylmao");
            Assert.Equal(1, openCount);
            Assert.Equal(0, getCount);
            Assert.Equal(1, setCount);
            Assert.False(pass);
            openCount = 0;
            getCount = 0;
            setCount = 0;
            handler = new IniSettingHandler(setting, mockFactory, logger);
            MockIniFile.nextSetResponder = (string section, string key, string value) => 
            {
                throw new ArgumentNullException();
            };
            pass = await handler.Apply("ayylmao");
            Assert.Equal(1, openCount);
            Assert.Equal(0, getCount);
            Assert.Equal(1, setCount);
            Assert.False(pass);
            openCount = 0;
            getCount = 0;
            setCount = 0;
            handler = new IniSettingHandler(setting, mockFactory, logger);
            MockIniFile.nextSetResponder = (string section, string key, string value) => 
            { 
                throw new SecurityException(); 
            };
            pass = await handler.Apply("ayylmao");
            Assert.Equal(1, openCount);
            Assert.Equal(0, getCount);
            Assert.Equal(1, setCount);
            Assert.False(pass);
            openCount = 0;
            getCount = 0;
            setCount = 0;
            handler = new IniSettingHandler(setting, mockFactory, logger);
            MockIniFile.nextSetResponder = (string section, string key, string value) => 
            {
                throw new ObjectDisposedException("ayylmao");
            };
            pass = await handler.Apply("ayylmao");
            Assert.Equal(1, openCount);
            Assert.Equal(0, getCount);
            Assert.Equal(1, setCount);
            Assert.False(pass);
            openCount = 0;
            getCount = 0;
            setCount = 0;
            handler = new IniSettingHandler(setting, mockFactory, logger);
            MockIniFile.nextSetResponder = (string section, string key, string value) =>
            { 
                throw new System.IO.IOException(); 
            };
            pass = await handler.Apply("ayylmao");
            Assert.Equal(1, openCount);
            Assert.Equal(0, getCount);
            Assert.Equal(1, setCount);
            Assert.False(pass);
            openCount = 0;
            getCount = 0;
            setCount = 0;
            handler = new IniSettingHandler(setting, mockFactory, logger);
            MockIniFile.nextSetResponder = (string section, string key, string value) =>
            {
                throw new UnauthorizedAccessException();
            };
            pass = await handler.Apply("ayylmao");
            Assert.Equal(1, openCount);
            Assert.Equal(0, getCount);
            Assert.Equal(1, setCount);
            Assert.False(pass);
        }
    }
}