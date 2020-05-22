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

#nullable enable

namespace Morphic.Settings.Tests
{
    public class ApplySessionTests
    {

        private class MockRegistry : IRegistry
        {
            public object? GetValue(string keyName, string valueName, object? defaultValue)
            {
                throw new NotImplementedException();
            }

            public bool SetValue(string keyName, string valueName, object? value, RegistryValueKind valueKind)
            {
                throw new NotImplementedException();
            }
        }

        private class MockSystemSetting: ISystemSetting
        {
            public string Id { get; private set; }

            public SettingType SettingType { get; private set; }

            public MockSystemSetting(string id)
            {
                Id = id;
                SettingType = SettingType.Boolean;
            }

            public Task SetValue(object value)
            {
                throw new NotImplementedException();
            }

            public Task<object?> GetValue()
            {
                throw new NotImplementedException();
            }
        }

        private class MockSystemSettingFactory : ISystemSettingFactory
        {
            public ISystemSetting Create(string id, IServiceProvider serviceProvider)
            {
                return new MockSystemSetting(id);
            }
        }


        [Fact]
        public async Task TestRun()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IServiceProvider>(provider => provider);
            services.AddSingleton<SettingsManager>();
            services.AddSingleton<IRegistry, MockRegistry>();
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
                        Name = "first",
                        Kind = Setting.ValueKind.Boolean,
                        Default = false,
                        HandlerDescription = new RegistrySettingHandlerDescription(@"HKEY_TEST\Test\Key", @"SomeValue", Microsoft.Win32.RegistryValueKind.DWord)
                    },
                    new Setting
                    {
                        Name = "second",
                        Kind = Setting.ValueKind.Integer,
                        Default = 0,
                        HandlerDescription = new SystemSettingHandlerDescription("SystemSetting_Morhpic_Test")
                    }
                }
            });
            var firstKey = new Preferences.Key("org.raisingthefloor.test", "first");
            var secondKey = new Preferences.Key("org.raisingthefloor.test", "second");
            var session = new ApplySession(settings, new Dictionary<Preferences.Key, object?>()
            {
                { firstKey, true },
                { secondKey, 12 }
            });

            // TODO: update MockRegistry and MockSystemSetting to track what calls they received
            // and add checks here to verify the expected calls were made
            var results = await session.Run();
            Assert.True(results.TryGetValue(firstKey, out var success));
            Assert.False(success);
            Assert.True(results.TryGetValue(secondKey, out success));
            Assert.False(success);
        }
    }
}

#nullable disable