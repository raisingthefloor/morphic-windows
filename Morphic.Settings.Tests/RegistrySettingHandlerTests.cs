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
using Microsoft.Win32;
using Morphic.Settings.Registry;
using System;
using System.Threading.Tasks;
using Xunit;

namespace Morphic.Settings.Tests
{

    public class RegistrySettingHandlerTests
    {

#nullable enable
        private class MockRegistry : IRegistry
        {

            public delegate object GetResponder(string keyName, string valueName, object? defaultValue);
            public delegate bool SetResponder(string keyName, string valueName, object? value, RegistryValueKind valueKind);

            public GetResponder NextGetResponder = (string keyName, string valueName, object? defaultValue) =>
            {
                throw new NotImplementedException();
            };

            public SetResponder NextSetResponder = (string keyName, string valueName, object? value, RegistryValueKind valueKind) =>
            {
                throw new NotImplementedException();
            };

            public object? GetValue(string keyName, string valueName, object? defaultValue)
            {
                return NextGetResponder(keyName, valueName, defaultValue);
            }

            public bool SetValue(string keyName, string valueName, object? value, RegistryValueKind valueKind)
            {
                return NextSetResponder(keyName, valueName, value, valueKind);
            }
        }
#nullable disable

        [Theory]
        [InlineData(Setting.ValueKind.String, RegistryValueKind.String, "Hello", "Hello")]
        [InlineData(Setting.ValueKind.String, RegistryValueKind.ExpandString, "Hello", "Hello")]
        [InlineData(Setting.ValueKind.Integer, RegistryValueKind.DWord, 52, 52L)]
        [InlineData(Setting.ValueKind.Integer, RegistryValueKind.QWord, 52L, 52L)]
        [InlineData(Setting.ValueKind.Boolean, RegistryValueKind.DWord, 1, true)]
        public async void TestCapturePass(Setting.ValueKind kind, RegistryValueKind rkind, object registryval, object finalval)
        {
            var registry = new MockRegistry();
            var loggerFactory = new LoggerFactory();
            var logger = loggerFactory.CreateLogger<RegistrySettingHandler>();

            var setting = new Setting()
            {
                Name = "test",
                Kind = kind,
                HandlerDescription = new RegistrySettingHandlerDescription(@"HKEY_TEST\Test\Key", "SomeValue", rkind)
            };
            var handler = new RegistrySettingHandler(setting, registry, logger);

            var callCount = 0;
            registry.NextGetResponder = (string keyName, string valueName, object defaultValue) =>
            {
                ++callCount;
                Assert.Equal(@"HKEY_TEST\Test\Key", keyName);
                Assert.Equal(@"SomeValue", valueName);
                Assert.Null(defaultValue);
                return registryval;
            };
            var result = await handler.Capture();
            Assert.Equal(1, callCount);
            Assert.True(result.Success);
            Assert.NotNull(result.Value);
            Assert.Equal(finalval, result.Value);
        }

        [Theory]
        [InlineData(Setting.ValueKind.Boolean, RegistryValueKind.String, "Hello")]
        [InlineData(Setting.ValueKind.Double, RegistryValueKind.String, "Hello")]
        [InlineData(Setting.ValueKind.Integer, RegistryValueKind.String, "Hello")]
        [InlineData(Setting.ValueKind.Double, RegistryValueKind.DWord, 52)]
        [InlineData(Setting.ValueKind.String, RegistryValueKind.DWord, 52)]
        [InlineData(Setting.ValueKind.Double, RegistryValueKind.QWord, 52L)]
        [InlineData(Setting.ValueKind.String, RegistryValueKind.QWord, 52L)]
        [InlineData(Setting.ValueKind.Boolean, RegistryValueKind.DWord, 3.14159d)]
        [InlineData(Setting.ValueKind.Integer, RegistryValueKind.DWord, 3.14159d)]
        [InlineData(Setting.ValueKind.String, RegistryValueKind.DWord, 3.14159d)]
        [InlineData(Setting.ValueKind.Boolean, RegistryValueKind.DWord, true)]
        [InlineData(Setting.ValueKind.Double, RegistryValueKind.DWord, true)]
        [InlineData(Setting.ValueKind.Integer, RegistryValueKind.DWord, true)]
        [InlineData(Setting.ValueKind.String, RegistryValueKind.DWord, true)]
        public async void TestCaptureFail(Setting.ValueKind kind, RegistryValueKind rkind, object registryval)
        {
            var registry = new MockRegistry();
            var loggerFactory = new LoggerFactory();
            var logger = loggerFactory.CreateLogger<RegistrySettingHandler>();

            var setting = new Setting()
            {
                Name = "test",
                Kind = kind,
                HandlerDescription = new RegistrySettingHandlerDescription(@"HKEY_TEST\Test\Key", "SomeValue", rkind)
            };
            var handler = new RegistrySettingHandler(setting, registry, logger);

            var callCount = 0;
            registry.NextGetResponder = (string keyName, string valueName, object defaultValue) =>
            {
                ++callCount;
                Assert.Equal(@"HKEY_TEST\Test\Key", keyName);
                Assert.Equal(@"SomeValue", valueName);
                Assert.Null(defaultValue);
                return registryval;
            };
            var result = await handler.Capture();
            Assert.Equal(1, callCount);
            Assert.False(result.Success);
            Assert.Null(result.Value);
        }

        [Fact]
        public async void TestCaptureExceptions()
        {
            var registry = new MockRegistry();
            var loggerFactory = new LoggerFactory();
            var logger = loggerFactory.CreateLogger<RegistrySettingHandler>();

            var setting = new Setting()
            {
                Name = "test",
                Kind = Setting.ValueKind.String,
                HandlerDescription = new RegistrySettingHandlerDescription(@"HKEY_TEST\Test\Key", "SomeValue", RegistryValueKind.String)
            };
            var handler = new RegistrySettingHandler(setting, registry, logger);

            var callCount = 0;
            registry.NextGetResponder = (string keyName, string valueName, object defaultvalue) =>
            {
                ++callCount;
                throw new System.Security.SecurityException();
            };
            var pass = await handler.Capture();
            Assert.Equal(1, callCount);
            Assert.False(pass.Success);
            Assert.Null(pass.Value);
            callCount = 0;
            registry.NextGetResponder = (string keyName, string valueName, object defaultvalue) =>
            {
                ++callCount;
                throw new ObjectDisposedException("ayylmao");
            };
            pass = await handler.Capture();
            Assert.Equal(1, callCount);
            Assert.False(pass.Success);
            Assert.Null(pass.Value);
            callCount = 0;
            registry.NextGetResponder = (string keyName, string valueName, object defaultvalue) =>
            {
                ++callCount;
                throw new System.IO.IOException();
            };
            pass = await handler.Capture();
            Assert.Equal(1, callCount);
            Assert.False(pass.Success);
            Assert.Null(pass.Value);
            callCount = 0;
            registry.NextGetResponder = (string keyName, string valueName, object defaultvalue) =>
            {
                ++callCount;
                throw new ArgumentException();
            };
            pass = await handler.Capture();
            Assert.Equal(1, callCount);
            Assert.False(pass.Success);
            Assert.Null(pass.Value);
            callCount = 0;
            registry.NextGetResponder = (string keyName, string valueName, object defaultvalue) =>
            {
                ++callCount;
                throw new ArgumentNullException();
            };
            pass = await handler.Capture();
            Assert.Equal(1, callCount);
            Assert.False(pass.Success);
            Assert.Null(pass.Value);
            callCount = 0;
            registry.NextGetResponder = (string keyName, string valueName, object defaultvalue) =>
            {
                ++callCount;
                throw new UnauthorizedAccessException();
            };
            pass = await handler.Capture();
            Assert.Equal(1, callCount);
            Assert.False(pass.Success);
            Assert.Null(pass.Value);
        }


        [Theory]
        [InlineData(RegistryValueKind.String, "Hello", "Hello")]
        [InlineData(RegistryValueKind.ExpandString, "Hello", "Hello")]
        [InlineData(RegistryValueKind.DWord, 52L, 52)]
        [InlineData(RegistryValueKind.QWord, 52L, 52L)]
        [InlineData(RegistryValueKind.DWord, true, 1)]
        public async void TestApplyPass(RegistryValueKind rkind, object initval, object registryval)
        {
            var registry = new MockRegistry();
            var loggerFactory = new LoggerFactory();
            var logger = loggerFactory.CreateLogger<RegistrySettingHandler>();

            var setting = new Setting()
            {
                Name = "test",
                Kind = Setting.ValueKind.Boolean,
                HandlerDescription = new RegistrySettingHandlerDescription(@"HKEY_TEST\Test\Key", "SomeValue", rkind)
            };
            var handler = new RegistrySettingHandler(setting, registry, logger);

            var callCount = 0;
            registry.NextSetResponder = (string keyName, string valueName, object value, RegistryValueKind valueKind) =>
            {
                ++callCount;
                Assert.Equal(@"HKEY_TEST\Test\Key", keyName);
                Assert.Equal(@"SomeValue", valueName);
                Assert.Equal(registryval, value);
                Assert.Equal(rkind, valueKind);
                return true;
            };
            var pass = await handler.Apply(initval);
            Assert.Equal(1, callCount);
            Assert.True(pass);
        }

        [Theory]
        [InlineData(RegistryValueKind.Binary, "Hello")]
        [InlineData(RegistryValueKind.DWord, "Hello")]
        [InlineData(RegistryValueKind.QWord, "Hello")]
        [InlineData(RegistryValueKind.MultiString, "Hello")]
        [InlineData(RegistryValueKind.None, "Hello")]
        [InlineData(RegistryValueKind.String, 52L)]
        [InlineData(RegistryValueKind.ExpandString, 52L)]
        [InlineData(RegistryValueKind.MultiString, 52L)]
        [InlineData(RegistryValueKind.Binary, 52L)]
        [InlineData(RegistryValueKind.None, 52L)]
        [InlineData(RegistryValueKind.DWord, 52)]
        [InlineData(RegistryValueKind.QWord, 52)]
        [InlineData(RegistryValueKind.Binary, 52)]
        [InlineData(RegistryValueKind.String, 52)]
        [InlineData(RegistryValueKind.ExpandString, 52)]
        [InlineData(RegistryValueKind.MultiString, 52)]
        [InlineData(RegistryValueKind.None, 52)]
        [InlineData(RegistryValueKind.QWord, true)]
        [InlineData(RegistryValueKind.Binary, true)]
        [InlineData(RegistryValueKind.String, true)]
        [InlineData(RegistryValueKind.ExpandString, true)]
        [InlineData(RegistryValueKind.MultiString, true)]
        [InlineData(RegistryValueKind.None, true)]
        public async void TestApplyFail(RegistryValueKind rkind, object initval)
        {
            var registry = new MockRegistry();
            var loggerFactory = new LoggerFactory();
            var logger = loggerFactory.CreateLogger<RegistrySettingHandler>();

            var setting = new Setting()
            {
                Name = "test",
                Kind = Setting.ValueKind.Boolean,
                HandlerDescription = new RegistrySettingHandlerDescription(@"HKEY_TEST\Test\Key", "SomeValue", rkind)
            };
            var handler = new RegistrySettingHandler(setting, registry, logger);

            var callCount = 0;
            registry.NextSetResponder = (string keyName, string valueName, object value, RegistryValueKind valueKind) =>
            {
                ++callCount;
                return true;
            };
            var pass = await handler.Apply(initval);
            Assert.Equal(0, callCount);
            Assert.False(pass);
        }

        [Fact]
        public async void TestApplyExceptions()
        {
            var registry = new MockRegistry();
            var loggerFactory = new LoggerFactory();
            var logger = loggerFactory.CreateLogger<RegistrySettingHandler>();

            var setting = new Setting()
            {
                Name = "test",
                Kind = Setting.ValueKind.String,
                HandlerDescription = new RegistrySettingHandlerDescription(@"HKEY_TEST\Test\Key", "SomeValue", RegistryValueKind.String)
            };
            var handler = new RegistrySettingHandler(setting, registry, logger);

            var callCount = 0;
            registry.NextSetResponder = (string keyName, string valueName, object value, RegistryValueKind valueKind) =>
            {
                ++callCount;
                throw new System.Security.SecurityException();
            };
            var pass = await handler.Apply("ayy");
            Assert.Equal(1, callCount);
            Assert.False(pass);
            callCount = 0;
            registry.NextSetResponder = (string keyName, string valueName, object value, RegistryValueKind valueKind) =>
            {
                ++callCount;
                throw new ObjectDisposedException("ayylmao");
            };
            pass = await handler.Apply("ayy");
            Assert.Equal(1, callCount);
            Assert.False(pass);
            callCount = 0;
            registry.NextSetResponder = (string keyName, string valueName, object value, RegistryValueKind valueKind) =>
            {
                ++callCount;
                throw new System.IO.IOException();
            };
            pass = await handler.Apply("ayy");
            Assert.Equal(1, callCount);
            Assert.False(pass);
            callCount = 0;
            registry.NextSetResponder = (string keyName, string valueName, object value, RegistryValueKind valueKind) =>
            {
                ++callCount;
                throw new ArgumentException();
            };
            pass = await handler.Apply("ayy");
            Assert.Equal(1, callCount);
            Assert.False(pass);
            callCount = 0;
            registry.NextSetResponder = (string keyName, string valueName, object value, RegistryValueKind valueKind) =>
            {
                ++callCount;
                throw new ArgumentNullException();
            };
            pass = await handler.Apply("ayy");
            Assert.Equal(1, callCount);
            Assert.False(pass);
            callCount = 0;
            registry.NextSetResponder = (string keyName, string valueName, object value, RegistryValueKind valueKind) =>
            {
                ++callCount;
                throw new UnauthorizedAccessException();
            };
            pass = await handler.Apply("ayy");
            Assert.Equal(1, callCount);
            Assert.False(pass);
        }
    }
}