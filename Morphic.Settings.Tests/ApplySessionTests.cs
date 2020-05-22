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
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;


namespace Morphic.Settings.Tests
{
    public class ApplySessionTests
    {
        private static int callCount = 0;
        private static int getCount = 0;
        private static int setCount = 0;

        //lists of things to fail/crash in a given test
        private static string[] failList = new string[0];
        private static string[] crashList = new string[0];

#nullable enable
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
#nullable disable

        private class MockIniFile : IIniFile
        {
            public string GetValue(string section, string key)
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
            public bool Call(SystemParametersInfo.Action action, int parameter1, object parameter2, bool updateUserProfile = false, bool sendChange = false)
            {
                ++callCount;
                throw new ArgumentException();
            }
        }

        private class MockSystemSetting : ISystemSetting
        {
            public string Id { get; private set; }

            public MockSystemSetting(string id)
            {
                Id = id;
            }

            public async Task SetValue(object value)
            {
                ++setCount;
                await Task.Delay(0);
                if (crashList.Contains(this.Id)) throw new ArgumentException();
            }

            public async Task<object> GetValue()
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
        [InlineData(new string[] { }, new string[] { "Registry Alpha" })]
        [InlineData(new string[] { }, new string[] { "Registry Beta" })]
        [InlineData(new string[] { }, new string[] { "Registry Gamma" })]
        [InlineData(new string[] { }, new string[] { "Ini Alpha" })]
        [InlineData(new string[] { }, new string[] { "Ini Beta" })]
        [InlineData(new string[] { }, new string[] { "Ini Gamma" })]
        [InlineData(new string[] { }, new string[] { "System Alpha" })]
        [InlineData(new string[] { }, new string[] { "System Beta" })]
        [InlineData(new string[] { }, new string[] { "System Gamma" })]
        [InlineData(new string[] { }, new string[] { "Registry Alpha", "System Beta" })]
        [InlineData(new string[] { }, new string[] { "Registry Alpha", "Registry Beta", "Registry Gamma" })]
        [InlineData(new string[] { }, new string[] { "Ini Alpha", "Ini Beta", "Ini Gamma" })]
        [InlineData(new string[] { }, new string[] { "System Alpha", "System Beta", "System Gamma" })]
        [InlineData(new string[] { }, new string[] { "Registry Gamma", "Ini Gamma", "System Gamma" })]
        [InlineData(new string[] { }, new string[] { "Registry Alpha", "Registry Beta", "Registry Gamma", "Ini Alpha", "Ini Beta", "Ini Gamma", "Registry Gamma", "Ini Gamma", "System Gamma" })]
        public async void TestRun(string[] failList, string[] crashList)
        {
            getCount = 0;
            setCount = 0;
            callCount = 0;
            ApplySessionTests.failList = failList;
            ApplySessionTests.crashList = crashList;
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
                        HandlerDescription = new SystemSettingHandlerDescription("System Alpha", SystemValueKind.Boolean)
                    },
                    new Setting
                    {
                        Name = "System Beta",
                        Kind = Setting.ValueKind.Integer,
                        Default = 52L,
                        HandlerDescription = new SystemSettingHandlerDescription("System Beta", SystemValueKind.Integer)
                    },
                    new Setting
                    {
                        Name = "System Gamma",
                        Kind = Setting.ValueKind.String,
                        Default = "default",
                        HandlerDescription = new SystemSettingHandlerDescription("System Gamma", SystemValueKind.String),
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
            var prefdict = new Dictionary<Preferences.Key, object>()
            {
                { rega, true },
                { regb, 13L },
                { inia, true },
                { inib, 13L },
                { sysa, true },
                { sysb, 13L },
                { sysc, "test" },
                { inic, "test" },
                { regc, "test" }
            };
            var session = new ApplySession(settings, prefdict);
            var results = await session.Run();
            Assert.True(results.TryGetValue(rega, out var passed));
            Assert.Equal(!crashList.Contains("Registry Alpha"), passed);
            Assert.True(results.TryGetValue(regb, out passed));
            Assert.Equal(!crashList.Contains("Registry Beta"), passed);
            Assert.True(results.TryGetValue(regc, out passed));
            Assert.Equal(!crashList.Contains("Registry Gamma"), passed);
            Assert.True(results.TryGetValue(inia, out passed));
            Assert.Equal(!crashList.Contains("Ini Alpha"), passed);
            Assert.True(results.TryGetValue(inib, out passed));
            Assert.Equal(!crashList.Contains("Ini Beta"), passed);
            Assert.True(results.TryGetValue(inic, out passed));
            Assert.Equal(!crashList.Contains("Ini Gamma"), passed);
            Assert.True(results.TryGetValue(sysa, out passed));
            Assert.Equal(!crashList.Contains("System Alpha"), passed);
            Assert.True(results.TryGetValue(sysb, out passed));
            Assert.Equal(!crashList.Contains("System Beta"), passed);
            Assert.True(results.TryGetValue(sysc, out passed));
            Assert.Equal(!crashList.Contains("System Gamma"), passed);

            Assert.Equal(9, setCount);
            //finalizer only fires if tests that use it were successful
            Assert.Equal((crashList.Contains("Registry Gamma") && crashList.Contains("Ini Gamma") && crashList.Contains("System Gamma")) ? 0 : 1, callCount);
            Assert.Equal(0, getCount);

            //test other constructor
            var prefs = new Preferences();
            prefs.Set(rega, true);
            prefs.Set(regb, 13L);
            prefs.Set(inia, true);
            prefs.Set(inib, 13L);
            prefs.Set(sysa, true);
            prefs.Set(sysb, 13L);
            prefs.Set(sysc, "test");
            prefs.Set(inic, "test");
            prefs.Set(regc, "test");
            var other = new ApplySession(settings, prefs);
            Assert.Equal(true, other.ValuesByKey[rega]);
            Assert.Equal(13L, other.ValuesByKey[regb]);
            Assert.Equal("test", other.ValuesByKey[regc]);
            Assert.Equal(true, other.ValuesByKey[inia]);
            Assert.Equal(13L, other.ValuesByKey[inib]);
            Assert.Equal("test", other.ValuesByKey[inic]);
            Assert.Equal(true, other.ValuesByKey[sysa]);
            Assert.Equal(13L, other.ValuesByKey[sysb]);
            Assert.Equal("test", other.ValuesByKey[sysc]);
        }
    }
}