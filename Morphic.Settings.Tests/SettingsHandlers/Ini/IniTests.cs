using System;
using System.Threading.Tasks;

namespace Morphic.Settings.Tests.SettingsHandlers.Ini
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
    using Settings.SettingsHandlers.Ini;
    using Xunit;
    using Xunit.Abstractions;

    public class IniTests
    {
        private readonly ITestOutputHelper output;

        public IniTests(ITestOutputHelper testOutputHelper)
        {
            this.output = testOutputHelper;
        }

        [Theory]
        [InlineData("read-test-simple.ini", "\n")]
        [InlineData("read-test-simple.ini", "\r\n")]
        [InlineData("read-test.ini", "\n", Skip = "unsupported")]
        [InlineData("read-test.ini", "\r\n", Skip = "unsupported")]
        [InlineData("aero-theme.ini", "\r\n")]
        public void IniReadTest(string filename, string eol)
        {
            string iniFile = TestUtil.GetLocalFile(filename);

            Dictionary<string,string> expected = new Dictionary<string, string>();

            Regex getExpected = new Regex("^[#;]@ *{?(?<key>[^:]*)}?:{?(?<value>.*?)}?$");

            // test ini files contain the expected data, on lines that start with '#@' or ';@'.
            // Extract this json from the file.
            foreach (string line in File.ReadAllLines(iniFile))
            {
                if (line.StartsWith("#@") || line.StartsWith(";@"))
                {
                    Match match = getExpected.Match(line);
                    expected[match.Groups["key"].Value] = match.Groups["value"].Value.Replace("\\n", eol);
                }
            }

            // Read the ini data from the file
            Ini iniReader = new TestIni(ReplaceEol(File.ReadAllText(iniFile), eol));
            iniReader.Parse();

            Dictionary<string,string> data = iniReader.ReadData();

            // Compare the results
            foreach ((string key, string value) in expected)
            {
                if (data.TryGetValue(key, out string actualValue))
                {
                    if (actualValue != value)
                    {
                        this.output.WriteLine($"{key}: {value} != {actualValue}");
                    }
                    Assert.Equal(value, actualValue);
                }
                else
                {
                    this.output.WriteLine($"{key}: {value} (not found)");
                    Assert.True(true);
                }
            }

            List<string> wrong = data.Keys.Except(expected.Keys).ToList();
            wrong.ForEach(key => this.output.WriteLine($"{key}: {data[key]} (unexpected)"));

            Assert.Equal(expected.Count, data.Count);
        }

        /// <summary>Tests writing the same data does not change the ini file.</summary>
        [Theory]
        [InlineData("write-test-simple.ini")]
        [InlineData("write-test.ini", Skip = "unsupported")]
        public void IniFileWriteNoChange(string filename)
        {
            // Read the ini file
            string content = File.ReadAllText(TestUtil.GetLocalFile(filename));
            Ini reader = new TestIni(content);
            reader.Parse();
            Dictionary<string, string> data = reader.ReadData();

            // Write back the same data
            TestIni writer1 = new(content);
            writer1.Parse();
            writer1.WriteData(data);
            string? writtenData = writer1.Result;

            // Result should be the same
            Assert.Equal(content, writtenData);
        }

        /// <summary>Tests updating values, then writing the original values, does not change the ini file.</summary>
        [Theory]
        [InlineData("write-test-simple.ini")]
        [InlineData("write-test.ini", Skip = "unsupported")]
        public void IniFileWriteNoChangeRevert(string filename)
        {
            // Read the ini file.
            string originalContent = File.ReadAllText(TestUtil.GetLocalFile(filename));
            Ini reader = new TestIni(originalContent);
            reader.Parse();
            Dictionary<string, string> originalData = reader.ReadData();

            // Update all values.
            Dictionary<string, string> updatedData =
                originalData.ToDictionary(kv => kv.Key, kv => $"{kv.Value}-{kv.Value}".ToUpper());

            // WriteFile the new data.
            TestIni writer1 = new(originalContent);
            writer1.Parse();
            writer1.WriteData(updatedData);
            string? updatedContent = writer1.Result;

            // The content should have changed
            Assert.NotEqual(originalContent, updatedContent);

            // WriteFile back the original data
            TestIni writer2 = new(updatedContent);
            writer2.Parse();
            writer2.WriteData(originalData);
            string? revertedContent = writer2.Result;

            // Result should be the same - however, the indentation on some multi-line values are not preserved.
            // Remove these large indentations before comparing.
            string fixedOriginal = Regex.Replace(originalContent, " {9,}", "    ", RegexOptions.Multiline);

            Assert.Equal(fixedOriginal, revertedContent);
        }

        private static string ReplaceEol(string input, string eol)
        {
            string normalised = input.Replace("\r\n", "\n").Replace("\r", "\n");
            return eol != "\n" ? normalised.Replace("\n", eol) : normalised;
        }

        [Theory]
        [InlineData("write-test-simple.ini", "write-test-simple.expect.ini", "\r\n")]
        [InlineData("write-test-simple.ini", "write-test-simple.expect.ini", "\n", Skip = "unsupported")]
        [InlineData("write-test.ini", "write-test.expect.ini", "\n", Skip = "unsupported")]
        [InlineData("write-test.ini", "write-test.expect.ini", "\r\n", Skip = "unsupported")]
        private static void IniWriteTest(string filename, string expectedFilename, string eol)
        {
            // Read the ini file.
            string originalContent = ReplaceEol(File.ReadAllText(TestUtil.GetLocalFile(filename)), eol);

            Ini reader = new TestIni(originalContent);
            reader.Parse();
            Dictionary<string, string> originalData = reader.ReadData();

            Regex getKey = new Regex(@"^(?:.*\.)?([^.]+)$");

            // Update all values. ("key=value" => "key=abc key xyz")
            Dictionary<string, string> updatedData =
                originalData
                    .Select(kv =>
                    {
                        string key = getKey.Replace(kv.Key, "$1");
                        string newValue = "abc " + key + " xyz";
                        if (key.Contains("remove_"))
                        {
                            newValue = null;
                        }
                        else if (key.StartsWith("ml_"))
                        {
                            if (!int.TryParse(key[3].ToString(), out int num))
                            {
                                num = 3;
                            }

                            List<string> lines = new List<string>();
                            for (int i = 0; i < num; i++)
                            {
                                lines.Add($"{newValue} line{i}");
                            }

                            newValue = string.Join(eol, lines);
                        }

                        return new KeyValuePair<string, string>(kv.Key, newValue);
                    }).ToDictionary(kv => kv.Key, kv => kv.Value);

            // Make some specific changes
            updatedData["empty_me"] = "";
            updatedData["null_me"] = null;
            updatedData["number_me1"] = "42";
            updatedData["number_me2"] = "12345";
            updatedData["true_me1"] = "true";
            updatedData["true_me2"] = "true";

            // Add some new ones
            updatedData["new_empty"] = "";
            updatedData["new_null"] = null;
            updatedData["new_number"] = "42";
            updatedData["new_true"] = "true";
            updatedData["newKey1"] = "new value";

            // Change within sections
            updatedData["section1.newKey1"] = "new value 1, in section1";
            updatedData["section1.newKey2"] = "new value 2";

            // Add new sections
            updatedData["newSection1.newKey1"] = "new value 1, in new section 1";
            updatedData["newSection1.newKey3"] = "new value 3";

            updatedData["newSection2.newKey1"] = "new value 1, in new section 2";
            updatedData["newSection2.newKey4"] = "new value 4";

            updatedData["newSection2.newSubSection.newKey1"] = "new value 1, in new sub section";
            updatedData["newSection2.newSubSection.newKey5"] = "new value 5";

            // Write the new data
            TestIni writer = new(ReplaceEol(File.ReadAllText(TestUtil.GetLocalFile(filename)), eol));
            writer.Parse();
            writer.WriteData(updatedData);
            string? updatedContent = writer.Result;
            string expectedContent = ReplaceEol(File.ReadAllText(TestUtil.GetLocalFile(expectedFilename)), eol);

            Assert.Equal(expectedContent, updatedContent);
        }

        /// <summary>Tests reading a real file.</summary>
        [Theory]
        [InlineData("read-test-simple.ini", "final_section.complete", "yes")]
        public async void IniReadFileTest(string filename, string propertyPath, string expectedValue)
        {
            Ini ini = new();
            await ini.ReadFile(TestUtil.GetLocalFile(filename));

            string? lastValue = ini.GetValue(propertyPath);

            // The value should have been read as expected.
            Assert.Equal(expectedValue, lastValue);
        }

        /// <summary>Tests reading a non-existent file.</summary>
        [Fact]
        public async void IniReadFileNotExistsTest()
        {
            Ini ini = new();
            await ini.ReadFile("not-exist" + Environment.TickCount);

            Dictionary<string,string> data = ini.ReadData();

            // An empty set of properties.
            Assert.Empty(data);
        }

        private async Task<Ini> ReadTestFile(string path)
        {
            Ini ini = new();
            await ini.ReadFile(path);

            // Ensure something was loaded
            Dictionary<string,string> data = ini.ReadData();
            Assert.NotEmpty(data);

            return ini;
        }

        /// <summary>Tests writing to a file.</summary>
        [Fact]
        public async void IniWriteFileTest()
        {
            Ini ini = await this.ReadTestFile(TestUtil.GetLocalFile("read-test-simple.ini"));

            // Update something, and write it
            ini.SetValue("written_section.written_value", "it worked");
            string tempFile = Path.GetTempFileName();

            try
            {
                await ini.WriteFile(tempFile);

                string[] writtenContent = await File.ReadAllLinesAsync(tempFile);

                // File should contain the new value
                Assert.Contains("written_value=it worked", writtenContent);
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }
    }
}
