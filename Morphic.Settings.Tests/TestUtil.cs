#nullable enable
namespace Morphic.Settings.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Text;
    using System.Text.RegularExpressions;
    using DotNetWindowsRegistry;
    using Microsoft.Extensions.DependencyInjection;
    using Settings.SolutionsRegistry;
    using Xunit;

    public static class TestUtil
    {
        /// <summary>
        /// Gets the path to a file relative to the calling .cs file.
        /// </summary>
        public static string GetLocalFile(string filename, [CallerFilePath] string sourceFile = null!)
        {
            return Path.GetFullPath(filename, Path.GetDirectoryName(sourceFile) ?? string.Empty);
        }

        /// <summary>
        /// Gets content a file relative to the calling .cs file.
        /// </summary>
        public static string ReadLocalFile(string filename, [CallerFilePath] string sourceFile = null!)
        {
            // ReSharper disable once ExplicitCallerInfoArgument
            string path = GetLocalFile(filename, sourceFile);
            return File.ReadAllText(path);
        }

        public static IServiceProvider GetTestProvider(ServiceCollection? serviceCollection = null)
        {
            ServiceCollection services = serviceCollection ?? GetTestServices();
            ServiceProvider serviceProvider = services.BuildServiceProvider();
            return serviceProvider;
        }

        public static ServiceCollection GetTestServices()
        {
            ServiceCollection services = new ServiceCollection();
            services.AddLogging();
            services.AddSolutionsRegistryServices();
            services.AddSingleton<IServiceProvider>(provider => provider);

            // Replace with test implementations
            services.ReplaceSingleton<IRegistry>(a => new InMemoryRegistry());
            //services.ReplaceSingleton<IniFileReader>(a => new TestIniFileReader());

            return services;
        }

        public static string DictionaryToString(this Dictionary<string, object?> dict)
        {
            return dict.ToDictionary(kv => kv.Key, kv => kv.Value?.ToString() ?? string.Empty).DictionaryToString();
        }

        public static string DictionaryToString(this Dictionary<string, string> dict)
        {
            StringBuilder result = new StringBuilder();
            foreach ((string? key, string? value) in dict)
            {
                result.Append(key)
                    .Append("=")
                    .Append(value.Replace("\r\n", "\n").Replace("\n", "\\n"))
                    .AppendLine();
            }

            return result.ToString();
        }

        public static Dictionary<string, string> StringToDictionary(string input)
        {
            return Regex.Matches(input.Replace("\r", ""), "^(?<key>[^=\n]*)=(?<value>.*)$", RegexOptions.Multiline)
                .ToDictionary(m => m.Groups["key"].Value, m => m.Groups["value"].Value.Replace("\\n", "\n"));
        }

        public static void CompareDictionaries(Dictionary<string, string> expected,
            Dictionary<string, string> actual, Action<string>? writeLine = null)
        {
            foreach ((string key, string value) in expected)
            {
                if (actual.TryGetValue(key, out string? actualValue))
                {
                    if (actualValue != value)
                    {
                        writeLine?.Invoke($"{key}: {value} != {actualValue}");
                    }
                    Assert.Equal(value, actualValue);
                }
                else
                {
                    writeLine?.Invoke($"{key}: {value} (not found)");
                    Assert.True(true);
                }
            }

            List<string> wrong = actual.Keys.Except(expected.Keys).ToList();
            wrong.ForEach(key => writeLine?.Invoke($"{key}: {actual[key]} (unexpected)"));

            Assert.Equal(expected.Count, actual.Count);
        }
    }
}
