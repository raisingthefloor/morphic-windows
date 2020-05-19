using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Text;
using Xunit;
using Morphic.Core;
using Morphic.Settings.Registry;
using Morphic.Settings.SystemSettings;
using Microsoft.Win32;
using Morphic.Settings.Ini;
using Morphic.Settings.Spi;
using Microsoft.VisualBasic.CompilerServices;

#nullable enable

namespace Morphic.Settings.Tests
{
    public class ApplySessionTests
    {
        public static int callCount = 0;
        public static int getCount = 0;
        public static int setCount = 0;
        private class MockRegistry : IRegistry
        {
            public object? GetValue(string keyName, string valueName, object? defaultValue)
            {
                ++getCount;
                if (valueName == "PASS") return "correct";
                else if (valueName == "FAIL") return null;
                else throw new NotImplementedException();
            }

            public bool SetValue(string keyName, string valueName, object? value, RegistryValueKind valueKind)
            {
                ++setCount;
                if (valueName == "PASS") return true;
                else if (valueName == "FAIL") return false;
                else throw new NotImplementedException();
            }
        }

        private class MockIniFile : IIniFile
        {
            public object? GetValue(string section, string key)
            {
                ++getCount;
                if (section == "PASS") return "correct";
                else if (section == "FAIL") return null;
                else throw new NotImplementedException();
            }

            public void SetValue(string section, string key, string value)
            {
                ++setCount;
                if (section == "PASS") return;
                else if (section == "CRASH") throw new NotImplementedException();
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
                throw new NotImplementedException();
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
                if (this.Id == "PASS") return;
                else if (this.Id == "FAIL") return;
                else throw new NotImplementedException();
            }

            public async Task<object?> GetValue()
            {
                ++getCount;
                await Task.Delay(0);
                if (this.Id == "PASS") return "correct";
                else if (this.Id == "FAIL") return null;
                else throw new NotImplementedException();
            }
        }

        private class MockSystemSettingFactory : ISystemSettingFactory
        {
            public ISystemSetting Create(string id, IServiceProvider serviceProvider)
            {
                return new MockSystemSetting(id);
            }
        }


        [Theory]
        [InlineData(SettingHandlerDescription.HandlerKind.Registry, SettingFinalizerDescription.HandlerKind.SystemParametersInfo, Setting.ValueKind.Double, 3.14159d, "PASS", true, false)]
        [InlineData(SettingHandlerDescription.HandlerKind.Registry, SettingFinalizerDescription.HandlerKind.SystemParametersInfo, Setting.ValueKind.Double, 3.14159d, "FAIL", false, false)]
        [InlineData(SettingHandlerDescription.HandlerKind.Registry, SettingFinalizerDescription.HandlerKind.SystemParametersInfo, Setting.ValueKind.Double, 3.14159d, "CRASH", false, true)]
        [InlineData(SettingHandlerDescription.HandlerKind.Ini, SettingFinalizerDescription.HandlerKind.SystemParametersInfo, Setting.ValueKind.Integer, 52L, "PASS", true, false)]
        [InlineData(SettingHandlerDescription.HandlerKind.Ini, SettingFinalizerDescription.HandlerKind.SystemParametersInfo, Setting.ValueKind.Integer, 52L, "FAIL", false, false)]
        [InlineData(SettingHandlerDescription.HandlerKind.Ini, SettingFinalizerDescription.HandlerKind.SystemParametersInfo, Setting.ValueKind.Integer, 52L, "CRASH", false, true)]
        [InlineData(SettingHandlerDescription.HandlerKind.System, SettingFinalizerDescription.HandlerKind.SystemParametersInfo, Setting.ValueKind.String, "ayylmao", "PASS", true, false)]
        [InlineData(SettingHandlerDescription.HandlerKind.System, SettingFinalizerDescription.HandlerKind.SystemParametersInfo, Setting.ValueKind.String, "ayylmao", "FAIL", false, false)]
        [InlineData(SettingHandlerDescription.HandlerKind.System, SettingFinalizerDescription.HandlerKind.SystemParametersInfo, Setting.ValueKind.String, "ayylmao", "CRASH", false, true)]
        public async Task TestRun(SettingHandlerDescription.HandlerKind handlerKind, SettingFinalizerDescription.HandlerKind finalizerKind, Setting.ValueKind valueKind, object? value, string passfail, bool success, bool crash)
        {
            getCount = 0;
            setCount = 0;
            callCount = 0;
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IServiceProvider>(provider => provider);
            services.AddSingleton<SettingsManager>();
            services.AddSingleton<IRegistry, MockRegistry>();
            services.AddSingleton<IIniFileFactory, MockIniFileFactory>();
            services.AddSingleton<ISystemParametersInfo, MockSystemParametersInfo>();
            services.AddSingleton<ISystemSettingFactory, MockSystemSettingFactory>();
            var serviceProvider = services.BuildServiceProvider();

            Setting setting = new Setting();
            setting.Name = "key";
            setting.Kind = valueKind;
            setting.Default = value;
            switch(handlerKind)
            {
                case SettingHandlerDescription.HandlerKind.Registry:
                    setting.HandlerDescription = new RegistrySettingHandlerDescription(@"HKEY_TEST\Test\Key", @passfail, Microsoft.Win32.RegistryValueKind.DWord);
                    break;
                case SettingHandlerDescription.HandlerKind.Ini:
                    setting.HandlerDescription = new IniSettingHandlerDescription("thefile", passfail, "keyname");
                    break;
                case SettingHandlerDescription.HandlerKind.System:
                    setting.HandlerDescription = new SystemSettingHandlerDescription(passfail);
                    break;
            }
            switch(finalizerKind)
            {
                case SettingFinalizerDescription.HandlerKind.SystemParametersInfo:
                    setting.FinalizerDescription = new SystemParametersInfoSettingFinalizerDescription(SystemParametersInfo.Action.SetCursors);
                    break;
            }

            var settings = serviceProvider.GetRequiredService<SettingsManager>();
            settings.Add(new Solution
            {
                Id = "org.raisingthefloor.test",
                Settings = new Setting[]
                {
                    setting
                }
            });
            var firstKey = new Preferences.Key("org.raisingthefloor.test", "key");
            var session = new ApplySession(settings, new Dictionary<Preferences.Key, object?>()
            {
                { firstKey, value }
            });

            // TODO: update MockRegistry and MockSystemSetting to track what calls they received
            // and add checks here to verify the expected calls were made
            var results = await session.Run();
            Assert.True(results.TryGetValue(firstKey, out var passed));
            if(success)
            {
                Assert.True(passed);
                Assert.Equal(1, setCount);
                Assert.Equal(1, callCount);
                Assert.Equal(0, getCount);
            }
            else
            {
                if(crash)
                {
                    Assert.False(passed);
                }
                else
                {
                    Assert.True(passed);
                }
            }
        }
    }
}

#nullable disable