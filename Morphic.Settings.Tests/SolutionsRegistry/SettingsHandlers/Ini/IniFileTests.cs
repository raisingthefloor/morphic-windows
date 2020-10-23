namespace Morphic.Settings.Tests.SolutionsRegistry.SettingsHandlers.Ini
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
    using Settings.SolutionsRegistry.SettingsHandlers.Ini;
    using Xunit;
    using Xunit.Abstractions;

    public class IniFileTests
    {
        private readonly ITestOutputHelper output;

        public IniFileTests(ITestOutputHelper testOutputHelper)
        {
            this.output = testOutputHelper;
        }

        [Fact]
        public void IniFileReadLf()
        {
            DoReadTest("\n");
        }

        [Fact]
        public void IniFileReadCrLf()
        {
            DoReadTest("\r\n");
        }

        public void DoReadTest(string eol)
        {
            string iniFile = TestUtil.GetLocalFile("read-test.ini");

            Dictionary<string,string> expected = new Dictionary<string, string>();

            Regex getExpected = new Regex("^#@ *{?(?<key>[^:]*)}?:{?(?<value>.*?)}?$");

            // read-test.ini contains the expected data, on lines that start with '#@'.
            // Extract this json from the file.
            foreach (string line in File.ReadAllLines(iniFile))
            {
                if (line.StartsWith("#@"))
                {
                    Match match = getExpected.Match(line);
                    expected[match.Groups["key"].Value] = match.Groups["value"].Value.Replace("\\n", eol);
                }
            }

            // Read the ini data from the file
            IniFileReader iniReader = new TestIniFileReader(ReplaceEol(File.ReadAllText(iniFile), eol));

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
        [Fact]
        public void IniFileWriteNoChange()
        {
            // Read the ini file
            string content = File.ReadAllText(TestUtil.GetLocalFile("write-test.ini"));
            IniFileReader reader = new TestIniFileReader(content);
            Dictionary<string, string> data = reader.ReadData();

            // Write back the same data
            IniFileWriter writer1 = new TestIniFileWriter(content);
            string writtenData = writer1.Write(data).Result;

            // Result should be the same
            Assert.Equal(content, writtenData);
        }

        /// <summary>Tests updating values, then writing the original values, does not change the ini file.</summary>
        [Fact]
        public void IniFileWriteNoChangeRevert()
        {
            // Read the ini file.
            string originalContent = File.ReadAllText(TestUtil.GetLocalFile("write-test.ini"));
            IniFileReader reader = new TestIniFileReader(originalContent);
            Dictionary<string, string> originalData = reader.ReadData();

            // Update all values.
            Dictionary<string, string> updatedData =
                originalData.ToDictionary(kv => kv.Key, kv => $"{kv.Value}-{kv.Value}".ToUpper());

            // Write the new data.
            IniFileWriter writer1 = new TestIniFileWriter(originalContent);
            string updatedContent = writer1.Write(updatedData).Result;

            // The content should have changed
            Assert.NotEqual(originalContent, updatedContent);

            // Write back the original data
            IniFileWriter writer2 = new TestIniFileWriter(updatedContent);
            string revertedContent = writer2.Write(originalData).Result;

            // Result should be the same - however, the indentation on some multi-line values are not preserved.
            // Remove these large indentations before comparing.
            string fixedOriginal = Regex.Replace(originalContent, " {9,}", "    ", RegexOptions.Multiline);

            Assert.Equal(fixedOriginal, revertedContent);
        }

        [Fact]
        public void IniFileWriteCorrectDataLf()
        {
            DoWriteTest("\n");
        }

        [Fact]
        public void IniFileWriteCorrectDataCrLf()
        {
            DoWriteTest("\r\n");
        }

        private static string ReplaceEol(string input, string eol)
        {
            string normalised = input.Replace("\r\n", "\n").Replace("\r", "\n");
            return eol != "\n" ? normalised.Replace("\n", eol) : normalised;
        }

        private static void DoWriteTest(string eol)
        {
            // Read the ini file.
            string originalContent =
                ReplaceEol(File.ReadAllText(TestUtil.GetLocalFile("write-test.ini")), eol);

            IniFileReader reader = new TestIniFileReader(originalContent);
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
            IniFileWriter writer =
                new TestIniFileWriter(ReplaceEol(File.ReadAllText(TestUtil.GetLocalFile("write-test.ini")), eol));
            string updatedContent = writer.Write(updatedData).Result;
            string expectedContent = ReplaceEol(File.ReadAllText(TestUtil.GetLocalFile("write-test.expect.ini")), eol);

            Assert.Equal(expectedContent, updatedContent);
        }
    }
}
