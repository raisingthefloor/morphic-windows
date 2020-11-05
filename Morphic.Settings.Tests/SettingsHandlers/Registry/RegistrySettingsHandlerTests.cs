#nullable enable
namespace Morphic.Settings.Tests.SettingsHandlers.Registry
{
    using System;
    using DotNetWindowsRegistry;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Win32;
    using Settings.SettingsHandlers;
    using Settings.SolutionsRegistry;
    using Xunit;

    public class RegistrySettingsHandlerTests
    {
        /// <summary>
        /// Tests getting with the registry handler.
        /// </summary>
        [Fact]
        public async void ReadTest()
        {
            InMemoryRegistry registry = new InMemoryRegistry();

            ServiceCollection services = TestUtil.GetTestServices();
            services.ReplaceSingleton<IRegistry>(p => registry);
            IServiceProvider serviceProvider = services.BuildServiceProvider();

            Solutions sr = Solutions.FromFile(serviceProvider, TestUtil.GetLocalFile("../test-solutions.json5"));

            // Test reading, with some values set
            string input = @"HKEY_CURRENT_USER
  test
    path
      settingOne = abc
      settingTwo = xyz";
            registry.AddStructure(RegistryView.Default, input);

            (string settingId, object? expect)[] tests = {
                ("setting1", "abc"),
                ("setting2", "xyz"),
                ("setting3", null),
            };

            foreach ((string settingId, object? expect) in tests)
            {
                Setting setting = sr.GetSetting("registry", settingId);

                // Get the value
                object? value = await setting.GetValue();

                Assert.Equal(expect, value);
            }

            // Ensure registry wasn't modified
            string print = registry.Print(RegistryView.Default);
            Assert.Equal(input, print);
        }

        /// <summary>
        /// Tests setting with the registry handler.
        /// </summary>
        [Fact]
        public async void WriteTest()
        {
            InMemoryRegistry registry = new InMemoryRegistry();

            ServiceCollection services = TestUtil.GetTestServices();
            services.ReplaceSingleton<IRegistry>(p => registry);
            IServiceProvider serviceProvider = services.BuildServiceProvider();

            Solutions sr = Solutions.FromFile(serviceProvider, TestUtil.GetLocalFile("../test-solutions.json5"));

            // Test reading, with some values set
            const string input = @"HKEY_CURRENT_USER
  test
    path
      settingOne = first initial value
      settingTwo = second initial value";
            registry.AddStructure(RegistryView.Default, input);

            (string settingId, object? newValue)[] tests = {
                ("setting1", "updated one"),
                ("setting2", "updated two"),
                ("setting3", "new value"),
            };

            foreach ((string settingId, object? newValue) in tests)
            {
                Setting setting = sr.GetSetting("registry", settingId);

                // Set the value
                await setting.SetValue(newValue);

                // Get the value
                object? value = await setting.GetValue();

                Assert.Equal(newValue, value);
            }

            // Ensure registry was updated correctly.
            string print = registry.Print(RegistryView.Default);
            const string expect = @"HKEY_CURRENT_USER
  test
    path
      settingOne = updated one
      settingThree = new value
      settingTwo = updated two";

            Assert.Equal(expect, print);

        }

    }
}
