namespace Morphic.Settings.Resolvers
{
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Resolves expander expressions in strings with a live value.
    /// Expressions can look like ${resolverName} or ${resolverName:value} or ${resolverName:value?default}, which
    /// get replaced with the value, or empty string if the value can't be retrieved.
    /// </summary>
    public abstract class Resolver
    {
        private static readonly Dictionary<string, Resolver> Resolvers = new Dictionary<string, Resolver>()
        {
            { "env", new EnvironmentResolver() },
            { "reg", new RegistryResolver() },
            { "folder", new FolderResolver() }
        };

        /// <summary>Adds a resolver to the global resolvers.</summary>
        public static void AddResolver(string name, Resolver resolver)
        {
            Resolver.Resolvers.Add(name, resolver);
        }

        /// <summary>Remove a resolver from the global resolvers.</summary>
        public static void RemoveResolver(string name)
        {
            Resolver.Resolvers.Remove(name);
        }

        private static Resolver? GetResolver(string name)
        {
            return Resolver.Resolvers.TryGetValue(name, out Resolver? resolver)
                ? resolver
                : null;
        }

        public static bool ContainsExpression([NotNullWhen(true)] string? input)
        {
            return input?.Contains("${") ?? false;
        }

        /// <summary>Resolves the given value.</summary>
        public abstract string? ResolveValue(string valueName);

        // expressions look like: ${resolverName} or ${resolverName:value} or ${resolverName:value?default}
        private static readonly Regex GetExpressions = new Regex(
            @"
            \$\{
            # resolver name
            (?<resolver> [^:}?]+ )
            # value
            (:(?<value> [^}?]+ ))?
            # default value
            (\s*\?\s*(?<default>
              # matches nested expressions
              (
                \{ (?<bracket>)
              | \} (?<-bracket>)
              | [^{}]
              )+
              (?(bracket)(?!)) # only match if the brackets balance
            |
             [^}]*
            ))?
            \}
            ", RegexOptions.Compiled | RegexOptions.IgnorePatternWhitespace);

        public static string? Resolve(string? input)
        {
            if (!ContainsExpression(input))
            {
                return input;
            }

            return Resolver.GetExpressions.Replace(input, match =>
            {
                Resolver? resolver = Resolver.GetResolver(match.Groups["resolver"].Value);
                if (resolver == null)
                {
                    return match.Value;
                }
                else
                {
                    string? result = resolver.ResolveValue(match.Groups["value"].Value);
                    if (result == null)
                    {
                        result = match.Groups["default"].Success
                            ? Resolver.Resolve(match.Groups["default"].Value)
                            : string.Empty;
                    }

                    return result;
                }
            });
        }
    }
}

