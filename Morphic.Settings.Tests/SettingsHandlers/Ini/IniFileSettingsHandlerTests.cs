#nullable enable
namespace Morphic.Settings.Tests.SettingsHandlers.Ini
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Core;
    using Microsoft.Extensions.DependencyInjection;
    using Settings.SettingsHandlers.Ini;
    using Settings.SolutionsRegistry;
    using Xunit;
    using Xunit.Abstractions;

    public class IniFileSettingsHandlerTests
    {
        private readonly ITestOutputHelper output;

        private const string TestContent = @"
ignoreA=AA
key1=valueOneA
key2=valueTwoA
key3=valueThreeA
[test1]
ignoreB=BB
key1=valueOneB
key2=valueTwoB
keyT1=valueT1
[test2]
ignoreC=CC
key1=valueOneParent
key2=valueTwoParent
keyT2=valueT2
[test3]
ignoreD=DD
key1=valueOneChild
key2=valueTwoChild
keyT3=valueT3
";

        public IniFileSettingsHandlerTests(ITestOutputHelper testOutputHelper)
        {
            this.output = testOutputHelper;
        }

        [Fact]
        public async Task IniGet()
        {
            TestIni ini = new(TestContent);

            ServiceCollection serviceCollection = TestUtil.GetTestServices();
            serviceCollection.ReplaceTransient<Ini>(s => ini);
            IServiceProvider serviceProvider = serviceCollection.BuildServiceProvider();

            Solutions sr = Solutions.FromFile(serviceProvider, TestUtil.GetLocalFile("..\\test-solutions.json5"));

            // Capture the preferences.
            Solution solution = sr.GetSolution("ini");
            SolutionPreferences solutionPreferences = new SolutionPreferences();

            var captureAsyncResult = await solution.CaptureAsync(solutionPreferences);
            if (captureAsyncResult.IsError == true)
            {
                throw new Exception("CaptureAsync failed with an error");
            }

            // The correct file path is used
            Assert.Equal("test-file.ini", ini.FilePath);

            Dictionary<string, string> expect = TestUtil.StringToDictionary(@"
settingA=valueOneA
settingB=valueTwoA
settingC=valueThreeA
settingD=valueOneB
settingE=valueTwoB
settingF=valueT1
settingG=valueOneParent
settingH=valueTwoParent
settingI=valueT2
settingJ=valueOneChild
settingK=valueTwoChild
settingL=valueT3
");

            Dictionary<string, string> actual =
                solutionPreferences.Values.ToDictionary(kv => kv.Key, kv => kv.Value?.ToString() ?? string.Empty);

            // Check the correct values are read.
            TestUtil.CompareDictionaries(expect, actual, this.output.WriteLine);
        }

        [Fact]
        public async Task IniSet()
        {
            TestIni writer = new(TestUtil.ReadLocalFile("handler-test.ini"));

            ServiceProvider serviceProvider = TestUtil.GetTestServices()
                .ReplaceTransient<Ini>(s => writer)
                .BuildServiceProvider();

            Solutions sr = Solutions.FromFile(serviceProvider, TestUtil.GetLocalFile("..\\test-solutions.json5"));

            Solution solution = sr.GetSolution("ini");
            SolutionPreferences solutionPreferences = new SolutionPreferences();

            // Set the new values
            Dictionary<string,string> newValues = TestUtil.StringToDictionary(@"
settingA=value-UPDATED-OneA
settingC=value-UPDATED-ThreeA
settingD=value-UPDATED-OneB
settingF=value-UPDATED-T1
settingG=value-UPDATED-OneParent
settingI=value-UPDATED-T2
settingJ=value-UPDATED-OneChild
settingL=value-UPDATED-T3
");

            foreach ((string? key, string? value) in newValues)
            {
                solutionPreferences.Values.Add(key, value);
            }

            // Apply the preferences.
            await solution.ApplyAsync(solutionPreferences);

            // Check the correct file path is used
            Assert.Equal("test-file.ini", writer.FilePath);
            // Check the file was saved
            Assert.True(writer.Saved, "INI file was saved");

            string iniContent = writer.Result!;

            // Check the new content is correct.
            string expected = @"ignoreA=AA
key1=value-UPDATED-OneA
key2=valueTwoA
key3=value-UPDATED-ThreeA
[test1]
ignoreB=BB
key1=value-UPDATED-OneB
key2=valueTwoB
keyT1=value-UPDATED-T1
[test2]
ignoreC=CC
key1=value-UPDATED-OneParent
key2=valueTwoParent
keyT2=value-UPDATED-T2
[test3]
ignoreD=DD
key1=value-UPDATED-OneChild
key2=valueTwoChild
keyT3=value-UPDATED-T3
";
            //File.WriteAllText(TestUtil.GetLocalFile("out.ini"), iniContent);
            Assert.Equal(expected, iniContent);

            return;
            ;

        }
    }

    /// <summary>
    /// Wrapper for the Ini class for testing.
    /// </summary>
    public class TestIni : Ini
    {
        /// <summary>
        /// Creates an instance using predefined content.
        /// </summary>
        /// <param name="content">The content.</param>
        public TestIni(string? content = null)
        {
            base.Content = content;
        }

        /// <summary>true if WriteFile has been called.</summary>
        public bool Saved { get; private set; }

        /// <summary>The written ini file content.</summary>
        public string? Result => this.IniFile?.ToString();

        /// <inheritdoc cref="Ini.ReadFile"/>
        public override Task ReadFile(string path)
        {
            this.FilePath = path;
            this.Parse();
            return Task.FromResult(true);
        }

        /// <inheritdoc cref="Ini.ReadFile"/>
        public override Task WriteFile(string filePath)
        {
            this.Saved = true;
            return Task.FromResult(true);
        }

    }
}
