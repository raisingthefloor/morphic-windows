namespace Morphic.Settings.Resolvers
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// A resolver that handles special folder paths.
    ///
    /// Example: ${folder:ProgramFiles}
    /// </summary>
    public class FolderResolver : Resolver
    {
        private static readonly Dictionary<string, string> ExtraPaths = new Dictionary<string, string>();

        /// <summary>Adds a path lookup to the resolver.</summary>
        public static void AddPath(string name, string path)
        {
            FolderResolver.ExtraPaths[name] = path;
        }

        public override string? ResolveValue(string valueName)
        {
            if (FolderResolver.ExtraPaths.TryGetValue(valueName, out string? path))
            {
                return path;
            }
            return Enum.TryParse(valueName, true, out Environment.SpecialFolder folder)
                ? Environment.GetFolderPath(folder)
                : null;
        }
    }
}
