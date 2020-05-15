using Microsoft.Win32;
using MorphicCore;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using Xunit;

namespace MorphicSettings.Tests
{
    public class SettingHandlerDescriptionTests
    {
        [Fact]
        public void TestJsonDeserializeUnknown()
        {
            var options = new JsonSerializerOptions();
            options.Converters.Add(new JsonElementInferredTypeConverter());
            options.Converters.Add(new SettingHandlerDescription.JsonConverter());

            var json = JsonSerializer.Serialize(new Dictionary<string, object?>()
            {
                { "type", "invalid" }
            });
            var handler = JsonSerializer.Deserialize<SettingHandlerDescription>(json, options);
            Assert.NotNull(handler);
            Assert.Equal(SettingHandlerDescription.HandlerKind.Unknown, handler.Kind);
        }

        [Theory]
        [InlineData("ayy", "lmao", true)]
        [InlineData(null, "lmao", true)]
        [InlineData("ayy", null, true)]
        [InlineData(null, null, true)]
        public void TestJsonDeserializeClient(string? solution, string? preference, bool success)
        {
            var options = new JsonSerializerOptions();
            options.Converters.Add(new JsonElementInferredTypeConverter());
            options.Converters.Add(new SettingHandlerDescription.JsonConverter());

            var json = JsonSerializer.Serialize(new Dictionary<string, object?>()
            {
                { "type", "org.raisingthefloor.morphic.client" },
                { "solution", solution },
                { "preference", preference }
            });
            var handler = JsonSerializer.Deserialize<SettingHandlerDescription>(json, options);
            Assert.NotNull(handler);
            if (success)
            {
                Assert.Equal(SettingHandlerDescription.HandlerKind.Client, handler.Kind);
                Assert.IsType<ClientSettingHandlerDescription>(handler);
                ClientSettingHandlerDescription client = (ClientSettingHandlerDescription)handler;
                Assert.Equal(solution, client.Key.Solution);
                Assert.Equal(preference, client.Key.Preference);
            }
            else
            {
                Assert.Equal(SettingHandlerDescription.HandlerKind.Unknown, handler.Kind);
            }
        }

        [Theory]
        [InlineData("thekey", "thevalue", "binary", RegistryValueKind.Binary, true)]
        public void TestJsonDeserializeRegistry(string? keyName, string? valueName, string? valueType, RegistryValueKind valueKind, bool success)
        {
            var options = new JsonSerializerOptions();
            options.Converters.Add(new JsonElementInferredTypeConverter());
            options.Converters.Add(new SettingHandlerDescription.JsonConverter());

            var json = JsonSerializer.Serialize(new Dictionary<string, object?>()
            {
                { "type", "com.microsoft.windows.registry" },
                { "key_name", keyName },
                { "value_name", valueName },
                { "value_type", valueType }
            });
            var handler = JsonSerializer.Deserialize<SettingHandlerDescription>(json, options);
            Assert.NotNull(handler);
            if (success)
            {
                Assert.Equal(SettingHandlerDescription.HandlerKind.Registry, handler.Kind);
                Assert.IsType<RegistrySettingHandlerDescription>(handler);
                RegistrySettingHandlerDescription registry = (RegistrySettingHandlerDescription)handler;
                Assert.Equal(keyName, registry.KeyName);
                Assert.Equal(valueName, registry.ValueName);
                Assert.Equal(valueKind, registry.ValueKind);
            }
            else
            {
                Assert.Equal(SettingHandlerDescription.HandlerKind.Unknown, handler.Kind);
            }
        }

        [Theory]
        [InlineData(null, null, null, true)]
        public void TestJsonDeserializeINI(string? filename, string? section, string? key, bool success)
        {
            var options = new JsonSerializerOptions();
            options.Converters.Add(new JsonElementInferredTypeConverter());
            options.Converters.Add(new SettingHandlerDescription.JsonConverter());

            var json = JsonSerializer.Serialize(new Dictionary<string, object?>()
            {
                { "type", "com.microsoft.windows.ini" },
                { "filename", filename },
                { "section", section },
                { "key", key }
            });
            var handler = JsonSerializer.Deserialize<SettingHandlerDescription>(json, options);
            Assert.NotNull(handler);
            if (success)
            {
                Assert.Equal(SettingHandlerDescription.HandlerKind.Ini, handler.Kind);
                Assert.IsType<IniSettingHandlerDescription>(handler);
                IniSettingHandlerDescription ini = (IniSettingHandlerDescription)handler;
                Assert.Equal(filename, ini.Filename);
                Assert.Equal(section, ini.Section);
                Assert.Equal(key, ini.Key);
            }
            else
            {
                Assert.Equal(SettingHandlerDescription.HandlerKind.Unknown, handler.Kind);
            }
        }

        [Theory]
        [InlineData(null, true)]
        public void TestJsonDeserializeSystem( string? settingId, bool success)
        {
            var options = new JsonSerializerOptions();
            options.Converters.Add(new JsonElementInferredTypeConverter());
            options.Converters.Add(new SettingHandlerDescription.JsonConverter());

            var json = JsonSerializer.Serialize(new Dictionary<string, object?>()
            {
                { "type", "com.microsoft.windows.system" },
                { "setting_id", settingId }
            });
            var handler = JsonSerializer.Deserialize<SettingHandlerDescription>(json, options);
            Assert.NotNull(handler);
            if (success)
            {
                Assert.Equal(SettingHandlerDescription.HandlerKind.System, handler.Kind);
                Assert.IsType<SystemSettingHandlerDescription>(handler);
                SystemSettingHandlerDescription system = (SystemSettingHandlerDescription)handler;
                Assert.Equal(settingId, system.SettingId);
            }
            else
            {
                Assert.Equal(SettingHandlerDescription.HandlerKind.Unknown, handler.Kind);
            }
        }
    }
}
