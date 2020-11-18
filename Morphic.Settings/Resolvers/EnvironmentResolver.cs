namespace Morphic.Settings.Resolvers
{
    using System;

    /// <summary>
    /// A resolver that handles environment variables.
    ///
    /// Example: ${env:SystemRoot}
    /// </summary>
    public class EnvironmentResolver : Resolver
    {
        public override string? ResolveValue(string valueName)
        {
            return Environment.GetEnvironmentVariable(valueName);
        }
    }
}
