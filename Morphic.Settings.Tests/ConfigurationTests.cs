using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text;
using Xunit;
using Morphic.Settings.Ini;
using System.Linq;

namespace Morphic.Settings.Tests
{
    public class ConfigurationTests
    {

        [Fact]
        public void TestBasic()
        {
            Configuration configuration;
            string[] lines =
            {
                "[Test]",
                "Hello=World",
                "Second=2",
                "Third=true",
                "[Another]",
                "Hello=There",
                "One=1.5"
            };
            Assert.True(Configuration.TryParse(lines, out configuration));
            Assert.NotNull(configuration);
            var value = configuration.Get("Test", "Hello");
            Assert.NotNull(value);
            Assert.Equal("World", value);
            value = configuration.Get("Test", "Second");
            Assert.NotNull(value);
            Assert.Equal("2", value);
            value = configuration.Get("Test", "Third");
            Assert.NotNull(value);
            Assert.Equal("true", value);
            value = configuration.Get("Test", "One");
            Assert.Null(value);
            value = configuration.Get("Another", "Hello");
            Assert.NotNull(value);
            Assert.Equal("There", value);
            value = configuration.Get("Another", "One");
            Assert.NotNull(value);
            Assert.Equal("1.5", value);
            value = configuration.Get("Another", "Second");
            Assert.Null(value);
            value = configuration.Get("Missing", "Second");
            Assert.Null(value);

            lines = configuration.GetIniLines().ToArray();
            Assert.Equal(7, lines.Length);
            Assert.Equal("[Test]", lines[0]);
            Assert.Equal("Hello=World", lines[1]);
            Assert.Equal("Second=2", lines[2]);
            Assert.Equal("Third=true", lines[3]);
            Assert.Equal("[Another]", lines[4]);
            Assert.Equal("Hello=There", lines[5]);
            Assert.Equal("One=1.5", lines[6]);
        }

        [Fact]
        public void TestMixedKeyValueDelimiter()
        {
            Configuration configuration;
            string[] lines =
            {
                "[Test]",
                "Hello=World",
                "Second:2",
                "Third=true",
                "[Another]",
                "Hello:There",
                "One=1.5"
            };
            Assert.True(Configuration.TryParse(lines, out configuration));
            Assert.NotNull(configuration);
            var value = configuration.Get("Test", "Hello");
            Assert.NotNull(value);
            Assert.Equal("World", value);
            value = configuration.Get("Test", "Second");
            Assert.NotNull(value);
            Assert.Equal("2", value);
            value = configuration.Get("Test", "Third");
            Assert.NotNull(value);
            Assert.Equal("true", value);
            value = configuration.Get("Test", "One");
            Assert.Null(value);
            value = configuration.Get("Another", "Hello");
            Assert.NotNull(value);
            Assert.Equal("There", value);
            value = configuration.Get("Another", "One");
            Assert.NotNull(value);
            Assert.Equal("1.5", value);
            value = configuration.Get("Another", "Second");
            Assert.Null(value);
            value = configuration.Get("Missing", "Second");
            Assert.Null(value);

            lines = configuration.GetIniLines().ToArray();
            Assert.Equal(7, lines.Length);
            Assert.Equal("[Test]", lines[0]);
            Assert.Equal("Hello=World", lines[1]);
            Assert.Equal("Second:2", lines[2]);
            Assert.Equal("Third=true", lines[3]);
            Assert.Equal("[Another]", lines[4]);
            Assert.Equal("Hello:There", lines[5]);
            Assert.Equal("One=1.5", lines[6]);
        }

        [Fact]
        public void TestComments()
        {
            Configuration configuration;
            string[] lines =
            {
                "; This is a comment",
                ";so is this",
                "# and this",
                "[Test]",
                "#comment",
                "Hello=World",
                "Second=2",
                "; Comments can be anywhere",
                ";",
                "; ",
                "Third=true",
                "[Another]",
                ";;;;;;;;;;;;;;;;;;;;;;;;;",
                ";;; Multiple delimiters",
                "Hello=There",
                "########################",
                "##More##",
                "One=1.5",
                "#testing#"
            };
            Assert.True(Configuration.TryParse(lines, out configuration));
            Assert.NotNull(configuration);
            var value = configuration.Get("Test", "Hello");
            Assert.NotNull(value);
            Assert.Equal("World", value);
            value = configuration.Get("Test", "Second");
            Assert.NotNull(value);
            Assert.Equal("2", value);
            value = configuration.Get("Test", "Third");
            Assert.NotNull(value);
            Assert.Equal("true", value);
            value = configuration.Get("Test", "One");
            Assert.Null(value);
            value = configuration.Get("Another", "Hello");
            Assert.NotNull(value);
            Assert.Equal("There", value);
            value = configuration.Get("Another", "One");
            Assert.NotNull(value);
            Assert.Equal("1.5", value);
            value = configuration.Get("Another", "Second");
            Assert.Null(value);
            value = configuration.Get("Missing", "Second");
            Assert.Null(value);

            lines = configuration.GetIniLines().ToArray();
            Assert.Equal("; This is a comment", lines[0]);
            Assert.Equal(";so is this", lines[1]);
            Assert.Equal("# and this", lines[2]);
            Assert.Equal("[Test]", lines[3]);
            Assert.Equal("#comment", lines[4]);
            Assert.Equal("Hello=World", lines[5]);
            Assert.Equal("Second=2", lines[6]);
            Assert.Equal("; Comments can be anywhere", lines[7]);
            Assert.Equal(";", lines[8]);
            Assert.Equal("; ", lines[9]);
            Assert.Equal("Third=true", lines[10]);
            Assert.Equal("[Another]", lines[11]);
            Assert.Equal(";;;;;;;;;;;;;;;;;;;;;;;;;", lines[12]);
            Assert.Equal(";;; Multiple delimiters", lines[13]);
            Assert.Equal("Hello=There", lines[14]);
            Assert.Equal("########################", lines[15]);
            Assert.Equal("##More##", lines[16]);
            Assert.Equal("One=1.5", lines[17]);
            Assert.Equal("#testing#", lines[18]);
        }

        [Fact]
        public void TestWhitespace()
        {
            Configuration configuration;
            string[] lines =
            {
                "",
                "    ",
                "[ Test ]",
                "    ",
                "",
                "# Comment   ",
                "",
                "Hello = World",
                "Second  =    2",
                "Third  :  true    ",
                "",
                "",
                "; This is a really important section",
                "   ;;;;;;    ",
                "",
                "  ",
                "[      Another    ]    ",
                "    ",
                "Hello   :  There  ",
                "",
                "",
                "One= 1.5 ",
                "",
                "",
                "  ",
                ""
            };
            Assert.True(Configuration.TryParse(lines, out configuration));
            Assert.NotNull(configuration);
            var value = configuration.Get("Test", "Hello");
            Assert.NotNull(value);
            Assert.Equal("World", value);
            value = configuration.Get("Test", "Second");
            Assert.NotNull(value);
            Assert.Equal("2", value);
            value = configuration.Get("Test", "Third");
            Assert.NotNull(value);
            Assert.Equal("true", value);
            value = configuration.Get("Test", "One");
            Assert.Null(value);
            value = configuration.Get("Another", "Hello");
            Assert.NotNull(value);
            Assert.Equal("There", value);
            value = configuration.Get("Another", "One");
            Assert.NotNull(value);
            Assert.Equal("1.5", value);
            value = configuration.Get("Another", "Second");
            Assert.Null(value);
            value = configuration.Get("Missing", "Second");
            Assert.Null(value);

            Assert.Equal(26, lines.Length);
            Assert.Equal("", lines[0]);
            Assert.Equal("    ", lines[1]);
            Assert.Equal("[ Test ]", lines[2]);
            Assert.Equal("    ", lines[3]);
            Assert.Equal("", lines[4]);
            Assert.Equal("# Comment   ", lines[5]);
            Assert.Equal("", lines[6]);
            Assert.Equal("Hello = World", lines[7]);
            Assert.Equal("Second  =    2", lines[8]);
            Assert.Equal("Third  :  true    ", lines[9]);
            Assert.Equal("", lines[10]);
            Assert.Equal("", lines[11]);
            Assert.Equal("; This is a really important section", lines[12]);
            Assert.Equal("   ;;;;;;    ", lines[13]);
            Assert.Equal("", lines[14]);
            Assert.Equal("  ", lines[15]);
            Assert.Equal("[      Another    ]    ", lines[16]);
            Assert.Equal("    ", lines[17]);
            Assert.Equal("Hello   :  There  ", lines[18]);
            Assert.Equal("", lines[19]);
            Assert.Equal("", lines[20]);
            Assert.Equal("One= 1.5 ", lines[21]);
            Assert.Equal("", lines[22]);
            Assert.Equal("", lines[23]);
            Assert.Equal("  ", lines[24]);
            Assert.Equal("", lines[25]);
        }

        [Fact]
        public void TestIndentation()
        {
            Configuration configuration;
            string[] lines =
            {
                "[Test]",
                "  Hello=World",
                "  Second=2",
                "  Third=true",
                "  [Another]",
                "    Hello=There",
                "    One=1.5",
                " [Any]",
                "  Indentation=Is",
                " Just=Fine",
                "Because=It",
                "DoesNot=Matter"
            };
            Assert.True(Configuration.TryParse(lines, out configuration));
            Assert.NotNull(configuration);
            var value = configuration.Get("Test", "Hello");
            Assert.NotNull(value);
            Assert.Equal("World", value);
            value = configuration.Get("Test", "Second");
            Assert.NotNull(value);
            Assert.Equal("2", value);
            value = configuration.Get("Test", "Third");
            Assert.NotNull(value);
            Assert.Equal("true", value);
            value = configuration.Get("Test", "One");
            Assert.Null(value);
            value = configuration.Get("Another", "Hello");
            Assert.NotNull(value);
            Assert.Equal("There", value);
            value = configuration.Get("Another", "One");
            Assert.NotNull(value);
            Assert.Equal("1.5", value);
            value = configuration.Get("Another", "Second");
            Assert.Null(value);
            value = configuration.Get("Missing", "Second");
            Assert.Null(value);
            value = configuration.Get("Any", "Indentation");
            Assert.NotNull(value);
            Assert.Equal("Is", value);
            value = configuration.Get("Any", "Just");
            Assert.NotNull(value);
            Assert.Equal("Fine", value);
            value = configuration.Get("Any", "Because");
            Assert.NotNull(value);
            Assert.Equal("It", value);
            value = configuration.Get("Any", "DoesNot");
            Assert.NotNull(value);
            Assert.Equal("Matter", value);

            lines = configuration.GetIniLines().ToArray();
            Assert.Equal(12, lines.Length);
            Assert.Equal("[Test]", lines[0]);
            Assert.Equal("  Hello=World", lines[1]);
            Assert.Equal("  Second=2", lines[2]);
            Assert.Equal("  Third=true", lines[3]);
            Assert.Equal("  [Another]", lines[4]);
            Assert.Equal("    Hello=There", lines[5]);
            Assert.Equal("    One=1.5", lines[6]);
            Assert.Equal(" [Any]", lines[7]);
            Assert.Equal("  Indentation=Is", lines[8]);
            Assert.Equal(" Just=Fine", lines[9]);
            Assert.Equal("Because=It", lines[10]);
            Assert.Equal("DoesNot=Matter", lines[11]);
        }

        [Fact]
        public void TestMultiline()
        {
            Configuration configuration;
            string[] lines =
            {
                "[Test]",
                "Hello=World",
                "Second=2",
                " is greater than 1",
                " but less than 3",
                "Third=true",
                "[Another]",
                "Hello=There",
                "    what a nice day",
                "      for a walk",
                "One=1.5"
            };
            Assert.True(Configuration.TryParse(lines, out configuration));
            Assert.NotNull(configuration);
            var value = configuration.Get("Test", "Hello");
            Assert.NotNull(value);
            Assert.Equal("World", value);
            value = configuration.Get("Test", "Second");
            Assert.NotNull(value);
            Assert.Equal("2 is greater than 1 but less than 3", value);
            value = configuration.Get("Test", "Third");
            Assert.NotNull(value);
            Assert.Equal("true", value);
            value = configuration.Get("Test", "One");
            Assert.Null(value);
            value = configuration.Get("Another", "Hello");
            Assert.NotNull(value);
            Assert.Equal("There what a nice day for a walk", value);
            value = configuration.Get("Another", "One");
            Assert.NotNull(value);
            Assert.Equal("1.5", value);
            value = configuration.Get("Another", "Second");
            Assert.Null(value);
            value = configuration.Get("Missing", "Second");
            Assert.Null(value);

            lines = configuration.GetIniLines().ToArray();
            Assert.Equal(7, lines.Length);
            Assert.Equal("[Test]", lines[0]);
            Assert.Equal("Hello=World", lines[1]);
            Assert.Equal("Second=2 is greater than 1 but less than 3", lines[2]);
            Assert.Equal("Third=true", lines[3]);
            Assert.Equal("[Another]", lines[4]);
            Assert.Equal("Hello=There what a nice day for a walk", lines[5]);
            Assert.Equal("One=1.5", lines[6]);
        }

        [Fact]
        public void TestUpdates()
        {
            Configuration configuration;
            string[] lines =
            {
                ";; testconfig",
                "[Test]",
                " Hello=World",
                "Second :2",
                "Third = true",
                "",
                "  # This is a another section",
                "  [Another]",
                "    Hello: There ",
                "    One=1.5",
                ""
            };
            Assert.True(Configuration.TryParse(lines, out configuration));
            Assert.NotNull(configuration);

            configuration.Set("Test", "Hello", "Earth");
            configuration.Set("Test", "Second", "Two");
            configuration.Set("Test", "Third", "false");
            configuration.Set("Another", "Hello", "You");
            configuration.Set("Another", "One", "1.0");

            lines = configuration.GetIniLines().ToArray();
            Assert.Equal(11, lines.Length);
            Assert.Equal(";; testconfig", lines[0]);
            Assert.Equal("[Test]", lines[1]);
            Assert.Equal(" Hello=Earth", lines[2]);
            Assert.Equal("Second :Two", lines[3]);
            Assert.Equal("Third = false", lines[4]);
            Assert.Equal("", lines[5]);
            Assert.Equal("  # This is a another section", lines[6]);
            Assert.Equal("  [Another]", lines[7]);
            Assert.Equal("    Hello: You ", lines[8]);
            Assert.Equal("    One=1.0", lines[9]);
            Assert.Equal("", lines[10]);
        }

        [Fact]
        public void TestAdditions()
        {
            Configuration configuration;
            string[] lines =
            {
                ";; testconfig",
                "[Test]",
                " Hello=World",
                "Second :2",
                "Third = true ",
                "",
                "  # This is a another section",
                "  [Another]",
                "    Hello=There ",
                "    One: 1.5",
                ""
            };
            Assert.True(Configuration.TryParse(lines, out configuration));
            Assert.NotNull(configuration);

            configuration.Set("Test", "New", "Value");
            configuration.Set("Another", "Two", "2.2");
            configuration.Set("ThirdSection", "Indented", "yes");

            lines = configuration.GetIniLines().ToArray();
            Assert.Equal(15, lines.Length);
            Assert.Equal(";; testconfig", lines[0]);
            Assert.Equal("[Test]", lines[1]);
            Assert.Equal(" Hello=World", lines[2]);
            Assert.Equal("Second :2", lines[3]);
            Assert.Equal("Third = true ", lines[4]);
            Assert.Equal("New = Value", lines[5]);
            Assert.Equal("", lines[6]);
            Assert.Equal("  # This is a another section", lines[7]);
            Assert.Equal("  [Another]", lines[8]);
            Assert.Equal("    Hello=There ", lines[9]);
            Assert.Equal("    One: 1.5", lines[10]);
            Assert.Equal("    Two: 2.2", lines[11]);
            Assert.Equal("", lines[12]);
            Assert.Equal("[ThirdSection]", lines[13]);
            Assert.Equal("Indented=yes", lines[14]);
        }
    }
}
