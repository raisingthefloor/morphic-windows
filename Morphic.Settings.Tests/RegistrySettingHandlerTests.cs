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
        [InlineData(Setting.ValueKind.String, RegistryValueKind.String, typeof(string), "Hello", "Hello", true)]
        [InlineData(Setting.ValueKind.Integer, RegistryValueKind.DWord, typeof(Int64), 52, 52L, true)]
        //[InlineData(Setting.ValueKind.Double, RegistryValueKind.Binary, typeof(double), 3.14159d, true)]   Does registry not do doubles?
        [InlineData(Setting.ValueKind.Boolean, RegistryValueKind.DWord, typeof(bool), 1, true, true)]
        [InlineData(Setting.ValueKind.Boolean, RegistryValueKind.DWord, typeof(Int64), "whoopsie", true, false)]
        public async Task TestCapture(Setting.ValueKind kind, RegistryValueKind rkind, System.Type type, object registryval, object finalval, bool success)
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

            var result = await handler.Capture();
            Assert.False(result.Success);   //will fail due to null
            Assert.Null(result.Value);


            var callCount = 0;
            registry.NextGetResponder = (string keyName, string valueName, object? defaultValue) =>
            {
                ++callCount;
                Assert.Equal(@"HKEY_TEST\Test\Key", keyName);
                Assert.Equal(@"SomeValue", valueName);
                Assert.Null(defaultValue);
                return registryval;
            };
            result = await handler.Capture();
            Assert.Equal(1, callCount);
            if (success)
            {
                Assert.True(result.Success);
                Assert.NotNull(result.Value);
                Assert.Equal(result.Value!.GetType(), type);
                Assert.Equal(finalval, result.Value);
            }
            else
            {
                Assert.False(result.Success);
                Assert.Null(result.Value);
            }
        }

        [Theory]
        [InlineData(Setting.ValueKind.String, RegistryValueKind.String, "Hello", "Hello", true)]
        [InlineData(Setting.ValueKind.Integer, RegistryValueKind.DWord, 52L, 52L, true)]
        [InlineData(Setting.ValueKind.Boolean, RegistryValueKind.DWord, "whoopsie", true, false)]
        public async Task TestApply(Setting.ValueKind kind, RegistryValueKind rkind, object initval, object registryval, bool success)
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

            var pass = await handler.Apply("World");    //should fail, no callback
            Assert.False(pass);

            var callCount = 0;
            registry.NextSetResponder = (string keyName, string valueName, object? value, RegistryValueKind valueKind) =>
            {
                ++callCount;
                Assert.Equal(@"HKEY_TEST\Test\Key", keyName);
                Assert.Equal(@"SomeValue", valueName);
                Assert.Equal(registryval, value);
                Assert.Equal(rkind, valueKind);
                return true;
            };
            pass = await handler.Apply(initval);
            if (success)
            {
                Assert.Equal(1, callCount);
                Assert.True(pass);
            }
            else
            {
                Assert.False(pass);
            }

            callCount = 0;
            registry.NextSetResponder = (string keyName, string valueName, object? value, RegistryValueKind valueKind) =>
            {
                ++callCount;
                Assert.Equal(@"HKEY_TEST\Test\Key", keyName);
                Assert.Equal(@"SomeValue", valueName);
                Assert.Equal(1, value);
                Assert.Equal(rkind, valueKind);
                throw new ArgumentException();
            };
            pass = await handler.Apply(1);
            Assert.Equal(1, callCount);
            Assert.False(pass);
        }
    }
}