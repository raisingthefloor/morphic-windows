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

using Microsoft.Win32;
using Morphic.Core;
using Morphic.Settings.Files;
using Morphic.Settings.SystemSettings;
using Morphic.Settings.Ini;
using Morphic.Settings.Registry;
using Morphic.Settings.Process;
using System.Collections.Generic;
using System.Text.Json;
using Xunit;

namespace Morphic.Settings.Tests
{
    public class SettingHandlerDescriptionTests
    {
        [Fact]
        public void TestJsonDeserializeUnknown()
        {
            var options = new JsonSerializerOptions();
            options.Converters.Add(new JsonElementInferredTypeConverter());
            options.Converters.Add(new SettingHandlerDescription.JsonConverter());

            var json = JsonSerializer.Serialize(new Dictionary<string, object>()
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
        public void TestJsonDeserializeClient(string solution, string preference, bool success)
        {
            var options = new JsonSerializerOptions();
            options.Converters.Add(new JsonElementInferredTypeConverter());
            options.Converters.Add(new SettingHandlerDescription.JsonConverter());

            var json = JsonSerializer.Serialize(new Dictionary<string, object>()
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

        [Fact]
        public void TestEqualityOperatorClient()
        {
            var options = new JsonSerializerOptions();
            options.Converters.Add(new JsonElementInferredTypeConverter());
            options.Converters.Add(new SettingHandlerDescription.JsonConverter());

            var json = JsonSerializer.Serialize(new Dictionary<string, object>()
            {
                { "type", "org.raisingthefloor.morphic.client" },
                { "solution", "thesolution" },
                { "preference", "thepreference" }
            });
            ClientSettingHandlerDescription client = (ClientSettingHandlerDescription)JsonSerializer.Deserialize<SettingHandlerDescription>(json, options);
            ClientSettingHandlerDescription sameclient = new ClientSettingHandlerDescription(new Preferences.Key("thesolution", "thepreference"));
            ClientSettingHandlerDescription differentclient1 = new ClientSettingHandlerDescription(new Preferences.Key("differentsolution", "thepreference"));
            ClientSettingHandlerDescription differentclient2 = new ClientSettingHandlerDescription(new Preferences.Key("thesolution", "differentpreference"));
            Assert.NotNull(client);
            Assert.NotNull(sameclient);
            Assert.NotNull(differentclient1);
            Assert.NotNull(differentclient2);
            Assert.Equal(client, sameclient);
            Assert.NotEqual(client, differentclient1);
            Assert.NotEqual(client, differentclient2);
        }

        [Theory]
        [InlineData("thekey", "thevalue", "binary", RegistryValueKind.Binary, true)]
        [InlineData("ThEkEy", "ThEvAlUe", "BiNaRy", RegistryValueKind.Binary, true)]
        [InlineData("tHeKeY", "tHeVaLuE", "bInArY", RegistryValueKind.Binary, true)]
        [InlineData("THEKEY", "THEVALUE", "BINARY", RegistryValueKind.Binary, true)]
        [InlineData("thekey", "thevalue", "dword", RegistryValueKind.DWord, true)]
        [InlineData("thekey", "thevalue", "DwOrD", RegistryValueKind.DWord, true)]
        [InlineData("thekey", "thevalue", "dWoRd", RegistryValueKind.DWord, true)]
        [InlineData("thekey", "thevalue", "DWORD", RegistryValueKind.DWord, true)]
        [InlineData("thekey", "thevalue", "qword", RegistryValueKind.QWord, true)]
        [InlineData("thekey", "thevalue", "QwOrD", RegistryValueKind.QWord, true)]
        [InlineData("thekey", "thevalue", "qWoRd", RegistryValueKind.QWord, true)]
        [InlineData("thekey", "thevalue", "QWORD", RegistryValueKind.QWord, true)]
        [InlineData("thekey", "thevalue", "string", RegistryValueKind.String, true)]
        [InlineData("thekey", "thevalue", "StRiNg", RegistryValueKind.String, true)]
        [InlineData("thekey", "thevalue", "sTrInG", RegistryValueKind.String, true)]
        [InlineData("thekey", "thevalue", "STRING", RegistryValueKind.String, true)]
        [InlineData("thekey", "thevalue", "expandstring", RegistryValueKind.ExpandString, true)]
        [InlineData("thekey", "thevalue", "ExPaNdStRiNg", RegistryValueKind.ExpandString, true)]
        [InlineData("thekey", "thevalue", "eXpAnDsTrInG", RegistryValueKind.ExpandString, true)]
        [InlineData("thekey", "thevalue", "EXPANDSTRING", RegistryValueKind.ExpandString, true)]
        [InlineData("thekey", "thevalue", "multistring", RegistryValueKind.MultiString, true)]
        [InlineData("thekey", "thevalue", "MuLtIsTrInG", RegistryValueKind.MultiString, true)]
        [InlineData("thekey", "thevalue", "mUlTiStRiNg", RegistryValueKind.MultiString, true)]
        [InlineData("thekey", "thevalue", "MULTISTRING", RegistryValueKind.MultiString, true)]
        [InlineData("thekey", "thevalue", "notathing", RegistryValueKind.MultiString, false)]
        public void TestJsonDeserializeRegistry(string keyName, string valueName, string valueType, RegistryValueKind valueKind, bool success)
        {
            var options = new JsonSerializerOptions();
            options.Converters.Add(new JsonElementInferredTypeConverter());
            options.Converters.Add(new SettingHandlerDescription.JsonConverter());

            var json = JsonSerializer.Serialize(new Dictionary<string, object>()
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

        [Fact]
        public void TestEqualityOperatorRegistry()
        {
            var options = new JsonSerializerOptions();
            options.Converters.Add(new JsonElementInferredTypeConverter());
            options.Converters.Add(new SettingHandlerDescription.JsonConverter());

            var json = JsonSerializer.Serialize(new Dictionary<string, object>()
            {
                { "type", "com.microsoft.windows.registry" },
                { "key_name", "thekey" },
                { "value_name", "thevalue" },
                { "value_type", "binary" }
            });
            RegistrySettingHandlerDescription registry = (RegistrySettingHandlerDescription)JsonSerializer.Deserialize<SettingHandlerDescription>(json, options);
            RegistrySettingHandlerDescription sameregistry = new RegistrySettingHandlerDescription("thekey", "thevalue", RegistryValueKind.Binary);
            RegistrySettingHandlerDescription differentregistry1 = new RegistrySettingHandlerDescription("differentkey", "thevalue", RegistryValueKind.Binary);
            RegistrySettingHandlerDescription differentregistry2 = new RegistrySettingHandlerDescription("thekey", "differentvalue", RegistryValueKind.Binary);
            //RegistrySettingHandlerDescription differentregistry3 = new RegistrySettingHandlerDescription("thekey", "thevalue", RegistryValueKind.DWord);
            Assert.NotNull(registry);
            Assert.NotNull(sameregistry);
            Assert.NotNull(differentregistry1);
            Assert.NotNull(differentregistry2);
            //Assert.NotNull(differentregistry3);
            Assert.Equal(registry, sameregistry);
            Assert.NotEqual(registry, differentregistry1);
            Assert.NotEqual(registry, differentregistry2);
            //Assert.NotEqual(registry, differentregistry3);
        }

        [Theory]
        [InlineData(null, null, null, true)]
        [InlineData("thefile", "thesection", "thekey", true)]
        public void TestJsonDeserializeIni(string filename, string section, string key, bool success)
        {
            var options = new JsonSerializerOptions();
            options.Converters.Add(new JsonElementInferredTypeConverter());
            options.Converters.Add(new SettingHandlerDescription.JsonConverter());

            var json = JsonSerializer.Serialize(new Dictionary<string, object>()
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

        [Fact]
        public void TestEqualityOperatorIni()
        {
            var options = new JsonSerializerOptions();
            options.Converters.Add(new JsonElementInferredTypeConverter());
            options.Converters.Add(new SettingHandlerDescription.JsonConverter());

            var json = JsonSerializer.Serialize(new Dictionary<string, object>()
            {
                { "type", "com.microsoft.windows.ini" },
                { "filename", "thefile" },
                { "section", "thesection" },
                { "key", "thekey" }
            });
            IniSettingHandlerDescription ini = (IniSettingHandlerDescription)JsonSerializer.Deserialize<SettingHandlerDescription>(json, options);
            IniSettingHandlerDescription sameini = new IniSettingHandlerDescription("thefile", "thesection", "thekey");
            IniSettingHandlerDescription differentini1 = new IniSettingHandlerDescription("differentfile", "thesection", "thekey");
            IniSettingHandlerDescription differentini2 = new IniSettingHandlerDescription("thefile", "differentsection", "thekey");
            IniSettingHandlerDescription differentini3 = new IniSettingHandlerDescription("thefile", "thesection", "differentkey");
            Assert.NotNull(ini);
            Assert.NotNull(sameini);
            Assert.NotNull(differentini1);
            Assert.NotNull(differentini2);
            Assert.NotNull(differentini3);
            Assert.Equal(ini, sameini);
            Assert.NotEqual(ini, differentini1);
            Assert.NotEqual(ini, differentini2);
            Assert.NotEqual(ini, differentini3);
        }

        [Theory]
        [InlineData(null, "boolean", SystemValueKind.Boolean, true)]
        [InlineData("thesetting", "boolean", SystemValueKind.Boolean, true)]
        public void TestJsonDeserializeSystem( string settingId, string valueType, SystemValueKind valueKind, bool success)
        {
            var options = new JsonSerializerOptions();
            options.Converters.Add(new JsonElementInferredTypeConverter());
            options.Converters.Add(new SettingHandlerDescription.JsonConverter());

            var json = JsonSerializer.Serialize(new Dictionary<string, object>()
            {
                { "type", "com.microsoft.windows.system" },
                { "setting_id", settingId },
                { "value_type", valueType }
            });
            var handler = JsonSerializer.Deserialize<SettingHandlerDescription>(json, options);
            Assert.NotNull(handler);
            if (success)
            {
                Assert.Equal(SettingHandlerDescription.HandlerKind.System, handler.Kind);
                Assert.IsType<SystemSettingHandlerDescription>(handler);
                SystemSettingHandlerDescription system = (SystemSettingHandlerDescription)handler;
                Assert.Equal(settingId, system.SettingId);
                Assert.Equal(valueKind, system.ValueKind);
            }
            else
            {
                Assert.Equal(SettingHandlerDescription.HandlerKind.Unknown, handler.Kind);
            }
        }

        [Fact]
        public void TestEqualityOperatorSystem()
        {
            var options = new JsonSerializerOptions();
            options.Converters.Add(new JsonElementInferredTypeConverter());
            options.Converters.Add(new SettingHandlerDescription.JsonConverter());

            var json = JsonSerializer.Serialize(new Dictionary<string, object>()
            {
                { "type", "com.microsoft.windows.system" },
                { "setting_id", "thesetting" },
                { "value_type", "boolean" }
            });
            SystemSettingHandlerDescription system = (SystemSettingHandlerDescription)JsonSerializer.Deserialize<SettingHandlerDescription>(json, options);
            SystemSettingHandlerDescription samesystem = new SystemSettingHandlerDescription("thesetting", SystemSettings.SystemValueKind.Boolean);
            SystemSettingHandlerDescription differentsystem = new SystemSettingHandlerDescription("differentsetting", SystemSettings.SystemValueKind.Boolean);
            SystemSettingHandlerDescription wrongtype = new SystemSettingHandlerDescription("differentsetting", SystemSettings.SystemValueKind.Integer);
            Assert.NotNull(system);
            Assert.NotNull(samesystem);
            Assert.NotNull(wrongtype);
            Assert.NotNull(differentsystem);
            Assert.Equal(system, samesystem);
            Assert.NotEqual(system, differentsystem);
            Assert.NotEqual(system, wrongtype);
        }

        [Theory]
        [InlineData(@"C:\Program Files\App\Config", new string[] { @"test.cfg", @"*.ini" }, true)]
        [InlineData(@"$(APPDATA)\App\Config", new string[] { @"test.*", @"settings\*.ini" }, true)]
        public void TestJsonDeserializeFiles(string root, string[] files, bool success)
        {
            var options = new JsonSerializerOptions();
            options.Converters.Add(new JsonElementInferredTypeConverter());
            options.Converters.Add(new SettingHandlerDescription.JsonConverter());

            var json = JsonSerializer.Serialize(new Dictionary<string, object>()
            {
                { "type", "com.microsoft.windows.files" },
                { "root", root },
                { "files", files }
            });
            var handler = JsonSerializer.Deserialize<SettingHandlerDescription>(json, options);
            Assert.NotNull(handler);
            if (success)
            {
                Assert.Equal(SettingHandlerDescription.HandlerKind.Files, handler.Kind);
                Assert.IsType<FilesSettingHandlerDescription>(handler);
                FilesSettingHandlerDescription filesHandler = (FilesSettingHandlerDescription)handler;
                Assert.Equal(root, filesHandler.Root);
                Assert.Equal(files.Length, filesHandler.Files.Length);
                for (var i = 0; i < files.Length; ++i)
                {
                    Assert.Equal(files[i], filesHandler.Files[i]);
                }
            }
            else
            {
                Assert.Equal(SettingHandlerDescription.HandlerKind.Unknown, handler.Kind);
            }
        }

        [Fact]
        public void TestEqualityOperatorFiles()
        {
            var handler = new FilesSettingHandlerDescription(@"$(APPDATA)\App\Config", new string[] { @"test.*", @"settings\*.ini" });
            var handler2 = new FilesSettingHandlerDescription(@"$(APPDATA)\App\Config", new string[] { @"test.*", @"settings\*.ini" });
            Assert.Equal(handler, handler2);
            handler2 = new FilesSettingHandlerDescription(@"$(APPDATA)\App", new string[] { @"test.*", @"settings\*.ini" });
            Assert.NotEqual(handler, handler2);
            handler2 = new FilesSettingHandlerDescription(@"$(APPDATA)\App\Config", new string[] { @"settings\*.ini" });
            Assert.NotEqual(handler, handler2);
            handler2 = new FilesSettingHandlerDescription(@"$(APPDATA)\App\Config", new string[] { @"settings\*.ini", @"test.*" });
            Assert.Equal(handler, handler2);
        }

        [Theory]
        [InlineData("Test.exe", "running", ProcessState.Running)]
        [InlineData("Test.exe", "installed", ProcessState.Installed)]
        [InlineData("Test.exe", "Running", ProcessState.Running)]
        [InlineData("Test2.exe", "RUNNING", ProcessState.Running)]
        [InlineData("Test2.exe", "RuNnInG", ProcessState.Running)]
        public void TestJsonDeserializeProcess(string appPathKey, string stateString, ProcessState expectedState)
        {
            var options = new JsonSerializerOptions();
            options.Converters.Add(new JsonElementInferredTypeConverter());
            options.Converters.Add(new SettingHandlerDescription.JsonConverter());

            var json = JsonSerializer.Serialize(new Dictionary<string, object>()
            {
                { "type", "com.microsoft.windows.process" },
                { "app_path_key", appPathKey },
                { "state", stateString }
            });
            var handler = JsonSerializer.Deserialize<SettingHandlerDescription>(json, options);
            Assert.NotNull(handler);
            Assert.Equal(SettingHandlerDescription.HandlerKind.Process, handler.Kind);
            Assert.IsType<ProcessSettingHandlerDescription>(handler);
            var processHandler = (ProcessSettingHandlerDescription)handler;
            Assert.Equal(appPathKey, processHandler.AppPathKey);
            Assert.Equal(expectedState, processHandler.State);
        }

        [Theory]
        [InlineData("Test.exe", "bad")]
        [InlineData("Test.exe", null)]
        [InlineData("Test.exe", 1)]
        [InlineData("Test.exe", 1.2)]
        [InlineData("Test.exe", true)]
        [InlineData(null, "running")]
        [InlineData(1, "running")]
        [InlineData(1.2, "running")]
        [InlineData(true, "running")]
        public void TestJsonDeserializeProcessFail(object appPathKey, object stateString)
        {
            var options = new JsonSerializerOptions();
            options.Converters.Add(new JsonElementInferredTypeConverter());
            options.Converters.Add(new SettingHandlerDescription.JsonConverter());

            var json = JsonSerializer.Serialize(new Dictionary<string, object>()
            {
                { "type", "com.microsoft.windows.process" },
                { "app_path_key", appPathKey },
                { "state", stateString }
            });
            var handler = JsonSerializer.Deserialize<SettingHandlerDescription>(json, options);
            Assert.NotNull(handler);
            Assert.Equal(SettingHandlerDescription.HandlerKind.Unknown, handler.Kind);
        }

        [Fact]
        public void TestEqualityOperatorProcess()
        {
            var description1 = new ProcessSettingHandlerDescription(@"Test.exe", ProcessState.Running);
            var description2 = new ProcessSettingHandlerDescription(@"Test.exe", ProcessState.Running);
            Assert.Equal(description1, description2);

            description1 = new ProcessSettingHandlerDescription(@"Test.exe", ProcessState.Running);
            description2 = new ProcessSettingHandlerDescription(@"Test2.exe", ProcessState.Running);
            Assert.NotEqual(description1, description2);

            description1 = new ProcessSettingHandlerDescription(@"Test.exe", ProcessState.Running);
            description2 = new ProcessSettingHandlerDescription(@"Test.exe", ProcessState.Installed);
            Assert.NotEqual(description1, description2);

            description1 = new ProcessSettingHandlerDescription(@"Test.exe", ProcessState.Running);
            description2 = new ProcessSettingHandlerDescription(@"Test2.exe", ProcessState.Installed);
            Assert.NotEqual(description1, description2);

            description1 = new ProcessSettingHandlerDescription(@"Test.exe", ProcessState.Installed);
            description2 = new ProcessSettingHandlerDescription(@"Test.exe", ProcessState.Installed);
            Assert.Equal(description1, description2);
        }
    }
}
