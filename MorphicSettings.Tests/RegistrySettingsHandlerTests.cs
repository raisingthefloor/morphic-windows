using System;
using System.Threading.Tasks;
using Microsoft.Win32;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

#nullable enable

namespace MorphicSettings.Tests
{

    public class RegistrySettingsHandlerTests
    {

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

        [Fact]
        public async Task TestApply()
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
            registry.NextGetResponder = (string keyName, string valueName, object? defaultValue) =>
            {
                ++callCount;
                Assert.Equal(@"HKEY_TEST\Test\Key", keyName);
                Assert.Equal(@"SomeValue", valueName);
                Assert.Null(defaultValue);
                return "Hello";
            };
            var result = await handler.Capture();
            Assert.Equal(1, callCount);
            Assert.True(result.Success);
            Assert.NotNull(result.Value);
            Assert.IsType<string>(result.Value!);
            Assert.Equal("Hello", result.Value as string);

            callCount = 0;
            registry.NextSetResponder = (string keyName, string valueName, object? value, RegistryValueKind valueKind) =>
            {
                ++callCount;
                Assert.Equal(@"HKEY_TEST\Test\Key", keyName);
                Assert.Equal(@"SomeValue", valueName);
                Assert.Equal(@"World", value);
                Assert.Equal(RegistryValueKind.String, valueKind);
                return true;
            };
            var success = await handler.Apply("World");
            Assert.Equal(1, callCount);
            Assert.True(success);

            callCount = 0;
            registry.NextSetResponder = (string keyName, string valueName, object? value, RegistryValueKind valueKind) =>
            {
                ++callCount;
                Assert.Equal(@"HKEY_TEST\Test\Key", keyName);
                Assert.Equal(@"SomeValue", valueName);
                Assert.Equal(1, value);
                Assert.Equal(RegistryValueKind.String, valueKind);
                throw new ArgumentException();
            };
            success = await handler.Apply(1);
            Assert.Equal(1, callCount);
            Assert.False(success);
        }
    }
}

#nullable disable