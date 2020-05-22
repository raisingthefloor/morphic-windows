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

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using Morphic.Core;
using Morphic.Settings.Ini;
using Morphic.Settings.Registry;
using Morphic.Settings.Spi;
using Morphic.Settings.SystemSettings;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

#nullable enable

namespace Morphic.Settings.Tests
{
    public class CaptureSessionTests
    {
        private static int callCount = 0;
        private static int getCount = 0;
        private static int setCount = 0;

        //lists of things to fail/crash in a given test
        private static string[] failList = new string[0];
        private static string[] crashList = new string[0];

        private class MockRegistry : IRegistry
        {
            public object? GetValue(string keyName, string valueName, object? defaultValue)
            {
                ++getCount;
                if (failList.Contains(keyName)) return null;
                else if (crashList.Contains(keyName)) throw new ArgumentException();
                else return "correct";
            }

            public bool SetValue(string keyName, string valueName, object? value, RegistryValueKind valueKind)
            {
                ++setCount;
                if (failList.Contains(keyName)) return false;
                else if (crashList.Contains(keyName)) throw new ArgumentException();
                else return true;
            }
        }

        private class MockIniFile : IIniFile
        {
            public string? GetValue(string section, string key)
            {
                ++getCount;
                if (failList.Contains(key)) return null;
                else if (crashList.Contains(key)) throw new ArgumentException();
                else return "correct";
            }

            public void SetValue(string section, string key, string value)
            {
                ++setCount;
                if (crashList.Contains(key)) throw new ArgumentException();
            }
        }

        private class MockIniFileFactory : IIniFileFactory
        {
            public IIniFile Open(string s)
            {
                return new MockIniFile();
            }
        }

        private class MockSystemParametersInfo : ISystemParametersInfo
        {
            public bool Call(SystemParametersInfo.Action action, int parameter1, object? parameter2, bool updateUserProfile = false, bool sendChange = false)
            {
                ++callCount;
                throw new ArgumentException();
            }
        }

        private class MockSystemSetting : ISystemSetting
        {
            public string Id { get; private set; }

            public SettingType SettingType { get; private set; }

            public MockSystemSetting(string id)
            {
                Id = id;
                SettingType = SettingType.String;
            }

            public async Task SetValue(object value)
            {
                ++setCount;
                await Task.Delay(0);
                if (crashList.Contains(this.Id)) throw new ArgumentException();
            }

            public async Task<object?> GetValue()
            {
                ++getCount;
                await Task.Delay(0);
                if (failList.Contains(this.Id)) return null;
                else if (crashList.Contains(this.Id)) throw new ArgumentException();
                else return "correct";
            }
        }

        private class MockSystemSettingFactory : ISystemSettingFactory
        {
            public ISystemSetting Create(string id, IServiceProvider serviceProvider)
            {
                return new MockSystemSetting(id);
            }
        }

        //NOTE: do not make cases where anything is on both lists
        [Theory]
        [InlineData(new string[] { }, new string[] { })]
        [InlineData(new string[] { "Registry Alpha" }, new string[] { })]
        [InlineData(new string[] { }, new string[] { "Registry Alpha" })]
        [InlineData(new string[] { "Registry Beta" }, new string[] { })]
        [InlineData(new string[] { }, new string[] { "Registry Beta" })]
        [InlineData(new string[] { "Registry Gamma" }, new string[] { })]
        [InlineData(new string[] { }, new string[] { "Registry Gamma" })]
        [InlineData(new string[] { "Ini Alpha" }, new string[] { })]
        [InlineData(new string[] { }, new string[] { "Ini Alpha" })]
        [InlineData(new string[] { "Ini Beta" }, new string[] { })]
        [InlineData(new string[] { }, new string[] { "Ini Beta" })]
        [InlineData(new string[] { "Ini Gamma" }, new string[] { })]
        [InlineData(new string[] { }, new string[] { "Ini Gamma" })]
        [InlineData(new string[] { "System Alpha" }, new string[] { })]
        [InlineData(new string[] { }, new string[] { "System Alpha" })]
        [InlineData(new string[] { "System Beta" }, new string[] { })]
        [InlineData(new string[] { }, new string[] { "System Beta" })]
        [InlineData(new string[] { "System Gamma" }, new string[] { })]
        [InlineData(new string[] { }, new string[] { "System Gamma" })]
        [InlineData(new string[] { "Registry Alpha", "Registry Beta", "Registry Gamma", "Ini Alpha", "Ini Beta", "Ini Gamma", "Registry Gamma", "Ini Gamma", "System Gamma" }, new string[] { })]
        [InlineData(new string[] { }, new string[] { "Registry Alpha", "Registry Beta", "Registry Gamma", "Ini Alpha", "Ini Beta", "Ini Gamma", "Registry Gamma", "Ini Gamma", "System Gamma" })]
        public async void TestRun(string[] failList, string[] crashList)
        {
            getCount = 0;
            setCount = 0;
            callCount = 0;
            CaptureSessionTests.failList = failList;
            CaptureSessionTests.crashList = crashList;
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IServiceProvider>(provider => provider);
            services.AddSingleton<SettingsManager>();
            services.AddSingleton<IRegistry, MockRegistry>();
            services.AddSingleton<IIniFileFactory, MockIniFileFactory>();
            services.AddSingleton<ISystemParametersInfo, MockSystemParametersInfo>();
            services.AddSingleton<ISystemSettingFactory, MockSystemSettingFactory>();
            var serviceProvider = services.BuildServiceProvider();

            var settings = serviceProvider.GetRequiredService<SettingsManager>();
            settings.Add(new Solution
            {
                Id = "org.raisingthefloor.test",
                Settings = new Setting[]
                {
                    new Setting
                    {
                        Name = "Registry Alpha",
                        Kind = Setting.ValueKind.Boolean,
                        Default = true,
                        HandlerDescription = new RegistrySettingHandlerDescription("Registry Alpha", "thevalue", RegistryValueKind.DWord)
                    },
                    new Setting
                    {
                        Name = "Registry Beta",
                        Kind = Setting.ValueKind.Integer,
                        Default = 52L,
                        HandlerDescription = new RegistrySettingHandlerDescription("Registry Beta", "thevalue", RegistryValueKind.DWord)
                    },
                    new Setting
                    {
                        Name = "Registry Gamma",
                        Kind = Setting.ValueKind.String,
                        Default = "default",
                        HandlerDescription = new RegistrySettingHandlerDescription("Registry Gamma", "thevalue", RegistryValueKind.String),
                        FinalizerDescription = new SystemParametersInfoSettingFinalizerDescription(SystemParametersInfo.Action.SetCursors)
                    },
                    new Setting
                    {
                        Name = "Ini Alpha",
                        Kind = Setting.ValueKind.Boolean,
                        Default = true,
                        HandlerDescription = new IniSettingHandlerDescription("thefile", "thesection", "Ini Alpha")
                    },
                    new Setting
                    {
                        Name = "Ini Beta",
                        Kind = Setting.ValueKind.Integer,
                        Default = 52L,
                        HandlerDescription = new IniSettingHandlerDescription("thefile", "thesection", "Ini Beta")
                    },
                    new Setting
                    {
                        Name = "Ini Gamma",
                        Kind = Setting.ValueKind.String,
                        Default = "default",
                        HandlerDescription = new IniSettingHandlerDescription("thefile", "thesection", "Ini Gamma"),
                        FinalizerDescription = new SystemParametersInfoSettingFinalizerDescription(SystemParametersInfo.Action.SetCursors)
                    },
                    new Setting
                    {
                        Name = "System Alpha",
                        Kind = Setting.ValueKind.Boolean,
                        Default = true,
                        HandlerDescription = new SystemSettingHandlerDescription("System Alpha")
                    },
                    new Setting
                    {
                        Name = "System Beta",
                        Kind = Setting.ValueKind.Integer,
                        Default = 52L,
                        HandlerDescription = new SystemSettingHandlerDescription("System Beta")
                    },
                    new Setting
                    {
                        Name = "System Gamma",
                        Kind = Setting.ValueKind.String,
                        Default = "default",
                        HandlerDescription = new SystemSettingHandlerDescription("System Gamma"),
                        FinalizerDescription = new SystemParametersInfoSettingFinalizerDescription(SystemParametersInfo.Action.SetCursors)
                    }
                }
            });

            var rega = new Preferences.Key("org.raisingthefloor.test", "Registry Alpha");
            var regb = new Preferences.Key("org.raisingthefloor.test", "Registry Beta");
            var regc = new Preferences.Key("org.raisingthefloor.test", "Registry Gamma");
            var inia = new Preferences.Key("org.raisingthefloor.test", "Ini Alpha");
            var inib = new Preferences.Key("org.raisingthefloor.test", "Ini Beta");
            var inic = new Preferences.Key("org.raisingthefloor.test", "Ini Gamma");
            var sysa = new Preferences.Key("org.raisingthefloor.test", "System Alpha");
            var sysb = new Preferences.Key("org.raisingthefloor.test", "System Beta");
            var sysc = new Preferences.Key("org.raisingthefloor.test", "System Gamma");
            var prefs = new Preferences();
            prefs.Set(rega, "incorrect");
            prefs.Set(regb, "incorrect");
            prefs.Set(regc, "incorrect");
            prefs.Set(inia, "incorrect");
            prefs.Set(inib, "incorrect");
            prefs.Set(inic, "incorrect");
            prefs.Set(sysa, "incorrect");
            prefs.Set(sysb, "incorrect");
            prefs.Set(sysc, "incorrect");
            var session = new CaptureSession(settings, prefs);
            session.AddAllSolutions();
            await session.Run();
            var dict = session.Preferences.Default!["org.raisingthefloor.test"].Values;
            if (crashList.Contains("Registry Alpha"))   //registry is unique in refusing incorrectly typed responses
            {
                Assert.True(dict.ContainsKey("Registry Alpha"));
                Assert.Equal("incorrect", (string?)dict["Registry Alpha"]);
            }
            else if (failList.Contains("Registry Alpha"))
            {
                Assert.True(dict.ContainsKey("Registry Alpha"));
                Assert.Equal("incorrect", (string?)dict["Registry Alpha"]);
            }
            else
            {
                Assert.True(dict.ContainsKey("Registry Alpha"));
                Assert.Equal("incorrect", (string?)dict["Registry Alpha"]);
            }
            if (crashList.Contains("Registry Beta"))
            {
                Assert.True(dict.ContainsKey("Registry Beta"));
                Assert.Equal("incorrect", (string?)dict["Registry Beta"]);
            }
            else if (failList.Contains("Registry Beta"))
            {
                Assert.True(dict.ContainsKey("Registry Beta"));
                Assert.Equal("incorrect", (string?)dict["Registry Beta"]);
            }
            else
            {
                Assert.True(dict.ContainsKey("Registry Beta"));
                Assert.Equal("incorrect", (string?)dict["Registry Beta"]);
            }
            if (crashList.Contains("Registry Gamma"))
            {
                Assert.True(dict.ContainsKey("Registry Gamma"));
                Assert.Equal("incorrect", (string?)dict["Registry Gamma"]);
            }
            else if (failList.Contains("Registry Gamma"))
            {
                Assert.True(dict.ContainsKey("Registry Gamma"));
                Assert.Equal("incorrect", (string?)dict["Registry Gamma"]);
            }
            else
            {
                Assert.True(dict.ContainsKey("Registry Gamma"));
                Assert.Equal("correct", (string?)dict["Registry Gamma"]);
            }
            if (crashList.Contains("Ini Alpha"))
            {
                Assert.True(dict.ContainsKey("Ini Alpha"));
                Assert.Equal("incorrect", (string?)dict["Ini Alpha"]);
            }
            else if (failList.Contains("Ini Alpha"))
            {
                Assert.True(dict.ContainsKey("Ini Alpha"));
                Assert.Null((string?)dict["Ini Alpha"]);
            }
            else
            {
                Assert.True(dict.ContainsKey("Ini Alpha"));
                Assert.Equal("correct", (string?)dict["Ini Alpha"]);
            }
            if (crashList.Contains("Ini Beta"))
            {
                Assert.True(dict.ContainsKey("Ini Beta"));
                Assert.Equal("incorrect", (string?)dict["Ini Beta"]);
            }
            else if (failList.Contains("Ini Beta"))
            {
                Assert.True(dict.ContainsKey("Ini Beta"));
                Assert.Null((string?)dict["Ini Beta"]);
            }
            else
            {
                Assert.True(dict.ContainsKey("Ini Beta"));
                Assert.Equal("correct", (string?)dict["Ini Beta"]);
            }
            if (crashList.Contains("Ini Gamma"))
            {
                Assert.True(dict.ContainsKey("Ini Gamma"));
                Assert.Equal("incorrect", (string?)dict["Ini Gamma"]);
            }
            else if (failList.Contains("Ini Gamma"))
            {
                Assert.True(dict.ContainsKey("Ini Gamma"));
                Assert.Null((string?)dict["Ini Gamma"]);
            }
            else
            {
                Assert.True(dict.ContainsKey("Ini Gamma"));
                Assert.Equal("correct", (string?)dict["Ini Gamma"]);
            }
            if (crashList.Contains("System Alpha"))
            {
                Assert.True(dict.ContainsKey("System Alpha"));
                Assert.Equal("incorrect", (string?)dict["System Alpha"]);
            }
            else if (failList.Contains("System Alpha"))
            {
                Assert.True(dict.ContainsKey("System Alpha"));
                Assert.Null((string?)dict["System Alpha"]);
            }
            else
            {
                Assert.True(dict.ContainsKey("System Alpha"));
                Assert.Equal("correct", (string?)dict["System Alpha"]);
            }
            if (crashList.Contains("System Beta"))
            {
                Assert.True(dict.ContainsKey("System Beta"));
                Assert.Equal("incorrect", (string?)dict["System Beta"]);
            }
            else if (failList.Contains("System Beta"))
            {
                Assert.True(dict.ContainsKey("System Beta"));
                Assert.Null((string?)dict["System Beta"]);
            }
            else
            {
                Assert.True(dict.ContainsKey("System Beta"));
                Assert.Equal("correct", (string?)dict["System Beta"]);
            }
            if (crashList.Contains("System Gamma"))
            {
                Assert.True(dict.ContainsKey("System Gamma"));
                Assert.Equal("incorrect", (string?)dict["System Gamma"]);
            }
            else if (failList.Contains("System Gamma"))
            {
                Assert.True(dict.ContainsKey("System Gamma"));
                Assert.Null((string?)dict["System Gamma"]);
            }
            else
            {
                Assert.True(dict.ContainsKey("System Gamma"));
                Assert.Equal("correct", (string?)dict["System Gamma"]);
            }
            Assert.Equal(9, getCount);
            Assert.Equal(0, setCount);
            Assert.Equal(0, callCount);
        }
    }
}

#nullable disable