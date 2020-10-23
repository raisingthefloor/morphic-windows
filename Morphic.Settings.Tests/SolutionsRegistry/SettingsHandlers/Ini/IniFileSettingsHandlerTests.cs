#nullable enable
namespace Morphic.Settings.Tests.SolutionsRegistry.SettingsHandlers.Ini
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Core;
    using Microsoft.Extensions.DependencyInjection;
    using Settings.SolutionsRegistry;
    using Settings.SolutionsRegistry.SettingsHandlers.Ini;
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
[[test3]]
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
            TestIniFileReader reader = new TestIniFileReader() {
                ContentOverride = TestContent
            };

            ServiceCollection serviceCollection = TestUtil.GetTestServices();
            serviceCollection.ReplaceTransient<IniFileReader>(s => reader);
            IServiceProvider serviceProvider = serviceCollection.BuildServiceProvider();

            Solutions sr = Solutions.FromFile(serviceProvider, TestUtil.GetLocalFile("..\\test-solutions.json5"));

            // Capture the preferences.
            Solution solution = sr.GetSolution("ini");
            SolutionPreferences solutionPreferences = new SolutionPreferences();

            await solution.Capture(solutionPreferences);

            // The correct file path is used
            Assert.Equal("test-file.ini", reader.FilePath);

            Dictionary<string,string> expect = TestUtil.StringToDictionary(@"
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
            TestIniFileWriter writer = new TestIniFileWriter() {
                ContentOverride = TestUtil.ReadLocalFile("handler-test.ini")
            };

            ServiceProvider serviceProvider = TestUtil.GetTestServices()
                .ReplaceTransient<IniFileWriter>(s => writer)
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
            await solution.Apply(solutionPreferences);

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
[[test3]]
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

    public class TestIniFileReader : IniFileReader
    {
        public TestIniFileReader(string? content = null)
        {
            this.content = content;
        }

        public string? ContentOverride { get; set; }

        public override void SetFile(string path)
        {
            this.FilePath = path;
            this.content = this.ContentOverride;
        }
    }

    public class TestIniFileWriter : IniFileWriter
    {
        public TestIniFileWriter(string? content = null)
        {
            this.content = content;
        }

        public string? ContentOverride { get; set; }
        public bool Saved { get; set; }

        public override void SetFile(string path)
        {
            this.FilePath = path;
            this.content = this.ContentOverride;
        }

        public override Task Save()
        {
            this.Saved = true;
            return Task.FromResult(true);
        }

    }
}
