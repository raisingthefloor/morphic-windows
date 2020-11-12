namespace Morphic.Settings.Tests.Resolvers
{
    using System.Collections.Generic;
    using Settings.Resolvers;
    using Xunit;

    public class TestResolver : Resolver
    {
        public Dictionary<string, string> Values { get; set; } = new Dictionary<string, string>();

        public override string? ResolveValue(string valueName)
        {
            return this.Values.TryGetValue(valueName, out string? value)
                ? value
                : null;
        }
    }

    public class ResolverTests
    {
        private static TestResolver Resolver1 = new TestResolver()
        {
            Values = new Dictionary<string, string>()
            {
                { "value1", "first" },
                { "value2", "second" }
            }
        };

        public static List<object[]> CheckResolveValueData = new List<object[]>()
        {
            // Basic tests
            new object[] { Resolver1, "a", "a" },
            new object[] { Resolver1, "${test:value1}", "first" },
            new object[] { Resolver1, "${test:value2}", "second" },
            new object[] { Resolver1, "${test:unknown}", "" },

            // Default values
            new object[] { Resolver1, "${test:value1?defaultA}", "first" },
            new object[] { Resolver1, "${test:value2?defaultB}", "second" },
            new object[] { Resolver1, "${test:unknown?defaultC}", "defaultC" },

            // Resolvable default values
            new object[] { Resolver1, "${test:value1?${test:value2}}", "first" },
            new object[] { Resolver1, "${test:unknown?${test:value2}}", "second" },
            new object[] { Resolver1, "${test:unknown?${test:unknown2?hello}}", "hello" },
            new object[] { Resolver1, "${test:unknown?${test:unknown2?${test:value2}}}", "second" },
            new object[] { Resolver1, "${test:unknown?${test:unknown2?${test:value2?fail}}}", "second" },
            new object[] { Resolver1, "${test:unknown?A${test:unknown2?B${test:value2?fail}C}D}", "ABsecondCD" },

            // Surrounding text
            new object[] { Resolver1, "A${test:value1}", "Afirst" },
            new object[] { Resolver1, "${test:value1}A", "firstA" },
            new object[] { Resolver1, "A${test:value1}A", "AfirstA" },
            new object[] { Resolver1, "A${test:unknown}A", "AA" },
            new object[] { Resolver1, "A${test:unknown}A", "AA" },

            // Unmatched braces
            new object[] { Resolver1, "${test:value1", "${test:value1" },
            new object[] { Resolver1, "${test:value1?{}", "first" },
            new object[] { Resolver1, "${test:unknown?{}", "{" },
            new object[] { Resolver1, "${test:unknown?${}}}", "${}}" },
            new object[] { Resolver1, "${test:unknown?${test:value1}}}", "first}" },
            new object[] { Resolver1, "${test:unknown?{${test:value1}}}", "{first}" },
            new object[] { Resolver1, "${test:unknown?{${test:value1}}", "{${test:value1}" },

            // Multiple values.
            new object[] { Resolver1, "${test:value1}${test:value2}", "firstsecond" },
            new object[] { Resolver1, "1${test:value1}2${test:value2}3", "1first2second3" },
        };

        /// <summary>Test the resolver gives the expected result for the given input.</summary>
        [Theory]
        [MemberData(nameof(CheckResolveValueData))]
        public void CheckResolveValue(Resolver resolver, string input, string expect)
        {
            Resolver.AddResolver("test", resolver);

            try
            {
                string? result = Resolver.Resolve(input);
                Assert.Equal(expect, result);
            }
            finally
            {
                Resolver.RemoveResolver("test");
            }
        }

        /// <summary>Test the ResolvingString gives the expected result for the given input.</summary>
        [Theory]
        [MemberData(nameof(CheckResolveValueData))]
        public void TestResolvingString(Resolver resolver, string input, string expect)
        {
            Resolver.AddResolver("test", resolver);

            try
            {
                ResolvingString rs = input;
                string? result = rs;
                Assert.Equal(expect, result);
            }
            finally
            {
                Resolver.RemoveResolver("test");
            }
        }

        /// <summary>Tests that ResolveString are resolved when accessed.</summary>
        [Fact]
        public void TestResolvingStringIsLive()
        {
            Resolver.AddResolver("test", Resolver1);

            try
            {
                ResolvingString rs = "${test:changing}";

                Resolver1.Values["changing"] = "first value";
                string? result1 = rs;
                Assert.Equal("first value", result1);

                Resolver1.Values["changing"] = "updated value";
                string? result2 = rs;
                Assert.Equal("updated value", result2);
            }
            finally
            {
                Resolver.RemoveResolver("test");
            }
        }
    }
}
